// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Automation.Shared;
using AuthJanitor.Automation.Shared.MetaServices;
using AuthJanitor.Automation.Shared.Models;
using AuthJanitor.Automation.Shared.ViewModels;
using AuthJanitor.Providers;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthJanitor.Automation.AdminApi
{
    public class ScheduleRekeyingTasks : StorageIntegratedFunction
    {
        private readonly AuthJanitorServiceConfiguration _serviceConfiguration;
        private readonly ProviderManagerService _providerManager;
        private readonly EventDispatcherMetaService _eventDispatcherMetaService;

        public ScheduleRekeyingTasks(
            AuthJanitorServiceConfiguration serviceConfiguration,
            EventDispatcherMetaService eventDispatcherMetaService,
            ProviderManagerService providerManager,
            IDataStore<ManagedSecret> managedSecretStore,
            IDataStore<Resource> resourceStore,
            IDataStore<RekeyingTask> rekeyingTaskStore,
            Func<ManagedSecret, ManagedSecretViewModel> managedSecretViewModelDelegate,
            Func<Resource, ResourceViewModel> resourceViewModelDelegate,
            Func<RekeyingTask, RekeyingTaskViewModel> rekeyingTaskViewModelDelegate,
            Func<AuthJanitorProviderConfiguration, ProviderConfigurationViewModel> configViewModelDelegate,
            Func<ScheduleWindow, ScheduleWindowViewModel> scheduleViewModelDelegate,
            Func<LoadedProviderMetadata, LoadedProviderViewModel> providerViewModelDelegate) :
                base(managedSecretStore, resourceStore, rekeyingTaskStore, managedSecretViewModelDelegate, resourceViewModelDelegate, rekeyingTaskViewModelDelegate, configViewModelDelegate, scheduleViewModelDelegate, providerViewModelDelegate)
        {
            _serviceConfiguration = serviceConfiguration;
            _eventDispatcherMetaService = eventDispatcherMetaService;
            _providerManager = providerManager;
        }

        [FunctionName("ScheduleRekeyingTasks")]
        public async Task Run([TimerTrigger("0 */5 * * * *")]TimerInfo myTimer, ILogger log)
        {
            _ = myTimer; // unused but required for attribute

            log.LogInformation($"Scheduling Rekeying Tasks for upcoming ManagedSecret expirations");

            await ScheduleApprovalRequiredTasks(log);
            await ScheduleAutoRekeyingTasks(log);
        }

        public async Task ScheduleApprovalRequiredTasks(ILogger log)
        {
            var jitCandidates = await GetSecretsForRekeyingTask(
                TaskConfirmationStrategies.AdminSignsOffJustInTime,
                _serviceConfiguration.ApprovalRequiredLeadTimeHours);
            log.LogInformation("Creating {0} tasks for just-in-time administrator approval", jitCandidates.Count);
            await CreateAndNotify(jitCandidates.Select(s => CreateRekeyingTask(s, s.Expiry)));

            // TODO: Implement schedule of availability windows and adjust timing here to match...
            // ... e.g. if a ManagedSecret expires on a Thursday but schedule only allows key changes
            //     on weekends, expiry needs to be shifted backwards to the weekend prior to the Thursday
            //     expiry.
            var cachedCandidates = await GetSecretsForRekeyingTask(
                TaskConfirmationStrategies.AdminCachesSignOff,
                _serviceConfiguration.ApprovalRequiredLeadTimeHours);
            log.LogInformation("Creating {0} tasks for cached administrator approval", cachedCandidates.Count);
            await CreateAndNotify(cachedCandidates.Select(s => CreateRekeyingTask(s, s.Expiry)));
        }

        public async Task ScheduleAutoRekeyingTasks(ILogger log)
        {
            var jitCandidates = await GetSecretsForRekeyingTask(
                TaskConfirmationStrategies.AutomaticRekeyingAsNeeded,
                _serviceConfiguration.AutomaticRekeyableTaskCreationLeadTimeHours);
            log.LogInformation("Creating {0} tasks for just-in-time auto-rekeying", jitCandidates.Count);
            await CreateAndNotify(jitCandidates.Select(s => CreateRekeyingTask(s, s.Expiry)));

            // TODO: Implement schedule of availability windows and adjust timing here to match...
            // ... e.g. if a ManagedSecret expires on a Thursday but schedule only allows key changes
            //     on weekends, expiry needs to be shifted backwards to the weekend prior to the Thursday
            //     expiry.
            var cachedCandidates = await GetSecretsForRekeyingTask(
                TaskConfirmationStrategies.AutomaticRekeyingScheduled,
                _serviceConfiguration.AutomaticRekeyableTaskCreationLeadTimeHours);
            log.LogInformation("Creating {0} tasks for scheduled auto-rekeying", cachedCandidates.Count);
            await CreateAndNotify(cachedCandidates.Select(s => CreateRekeyingTask(s, s.Expiry)));
        }

        private RekeyingTask CreateRekeyingTask(ManagedSecret secret, DateTimeOffset expiry) =>
            new RekeyingTask()
            {
                ManagedSecretId = secret.ObjectId,
                Expiry = expiry,
                ConfirmationType = GetPreferredConfirmation(secret.TaskConfirmationStrategies),
                Queued = DateTimeOffset.UtcNow,
                RekeyingInProgress = false
            };

        private async Task CreateAndNotify(IEnumerable<RekeyingTask> tasks)
        {
            if (!tasks.Any()) return;
            await Task.WhenAll(tasks.Select(t => RekeyingTasks.CreateAsync(t)));

            foreach (var task in tasks)
            {
                var secret = await ManagedSecrets.GetAsync(task.ManagedSecretId);
                if (task.ConfirmationType.UsesOBOTokens())
                    await _eventDispatcherMetaService.DispatchEvent(AuthJanitorSystemEvents.RotationTaskCreatedForApproval, nameof(AdminApi.ScheduleRekeyingTasks.CreateAndNotify), task);
                else
                    await _eventDispatcherMetaService.DispatchEvent(AuthJanitorSystemEvents.RotationTaskCreatedForAutomation, nameof(AdminApi.ScheduleRekeyingTasks.CreateAndNotify), task);
            }
        }

        private TaskConfirmationStrategies GetPreferredConfirmation(TaskConfirmationStrategies taskConfirmationStrategy)
        {
            if (taskConfirmationStrategy.HasFlag(TaskConfirmationStrategies.AdminCachesSignOff) &&
                taskConfirmationStrategy.HasFlag(TaskConfirmationStrategies.AdminSignsOffJustInTime))
                return TaskConfirmationStrategies.AdminCachesSignOff;
            if (taskConfirmationStrategy.HasFlag(TaskConfirmationStrategies.AutomaticRekeyingAsNeeded) &&
                taskConfirmationStrategy.HasFlag(TaskConfirmationStrategies.AutomaticRekeyingScheduled))
                return TaskConfirmationStrategies.AutomaticRekeyingScheduled;
            return taskConfirmationStrategy;
        }

        private async Task<List<ManagedSecret>> GetSecretsForRekeyingTask(
            TaskConfirmationStrategies taskConfirmationStrategies,
            int leadTimeHours)
        {
            var secretsToRotate = await ManagedSecrets.GetAsync(s =>
                s.TaskConfirmationStrategies.HasFlag(taskConfirmationStrategies) &&
                s.Expiry < DateTimeOffset.UtcNow + TimeSpan.FromHours(leadTimeHours));

            var rekeyingTasks = await RekeyingTasks.ListAsync();
            return secretsToRotate
                        .Where(s => !rekeyingTasks.Any(t =>
                            t.ManagedSecretId == s.ObjectId &&
                            !t.RekeyingCompleted))
                        .ToList();
        }
    }
}