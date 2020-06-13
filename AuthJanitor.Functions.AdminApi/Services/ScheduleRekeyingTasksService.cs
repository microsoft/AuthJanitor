// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.UI.Shared;
using AuthJanitor.UI.Shared.MetaServices;
using AuthJanitor.UI.Shared.Models;
using AuthJanitor.EventSinks;
using AuthJanitor.Integrations.DataStores;
using AuthJanitor.Providers;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthJanitor.Services
{
    public class ScheduleRekeyingTasksService
    {
        private readonly AuthJanitorCoreConfiguration _configuration;
        private readonly ProviderManagerService _providerManager;
        private readonly EventDispatcherMetaService _eventDispatcherMetaService;

        private readonly IDataStore<ManagedSecret> _managedSecrets;
        private readonly IDataStore<Resource> _resources;
        private readonly IDataStore<RekeyingTask> _rekeyingTasks;

        public ScheduleRekeyingTasksService(
            IOptions<AuthJanitorCoreConfiguration> configuration,
            EventDispatcherMetaService eventDispatcherMetaService,
            ProviderManagerService providerManager,
            IDataStore<ManagedSecret> managedSecretStore,
            IDataStore<Resource> resourceStore,
            IDataStore<RekeyingTask> rekeyingTaskStore)
        {
            _configuration = configuration.Value;
            _eventDispatcherMetaService = eventDispatcherMetaService;
            _providerManager = providerManager;

            _managedSecrets = managedSecretStore;
            _resources = resourceStore;
            _rekeyingTasks = rekeyingTaskStore;
        }

        public async Task Run(TimerInfo myTimer, ILogger log)
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
                _configuration.ApprovalRequiredLeadTimeHours);
            log.LogInformation("Creating {TaskCount} tasks for just-in-time administrator approval", jitCandidates.Count);
            await CreateAndNotify(jitCandidates.Select(s => CreateRekeyingTask(s, s.Expiry)));

            // TODO: Implement schedule of availability windows and adjust timing here to match...
            // ... e.g. if a ManagedSecret expires on a Thursday but schedule only allows key changes
            //     on weekends, expiry needs to be shifted backwards to the weekend prior to the Thursday
            //     expiry.
            var cachedCandidates = await GetSecretsForRekeyingTask(
                TaskConfirmationStrategies.AdminCachesSignOff,
                _configuration.ApprovalRequiredLeadTimeHours);
            log.LogInformation("Creating {TaskCount} tasks for cached administrator approval", cachedCandidates.Count);
            await CreateAndNotify(cachedCandidates.Select(s => CreateRekeyingTask(s, s.Expiry)));
        }

        public async Task ScheduleAutoRekeyingTasks(ILogger log)
        {
            var jitCandidates = await GetSecretsForRekeyingTask(
                TaskConfirmationStrategies.AutomaticRekeyingAsNeeded,
                _configuration.AutomaticRekeyableTaskCreationLeadTimeHours);
            log.LogInformation("Creating {TaskCount} tasks for just-in-time auto-rekeying", jitCandidates.Count);
            await CreateAndNotify(jitCandidates.Select(s => CreateRekeyingTask(s, s.Expiry)));

            // TODO: Implement schedule of availability windows and adjust timing here to match...
            // ... e.g. if a ManagedSecret expires on a Thursday but schedule only allows key changes
            //     on weekends, expiry needs to be shifted backwards to the weekend prior to the Thursday
            //     expiry.
            var cachedCandidates = await GetSecretsForRekeyingTask(
                TaskConfirmationStrategies.AutomaticRekeyingScheduled,
                _configuration.AutomaticRekeyableTaskCreationLeadTimeHours);
            log.LogInformation("Creating {TaskCount} tasks for scheduled auto-rekeying", cachedCandidates.Count);
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
            await Task.WhenAll(tasks.Select(t => _rekeyingTasks.Create(t)));

            foreach (var task in tasks)
            {
                var secret = await _managedSecrets.GetOne(task.ManagedSecretId);
                if (task.ConfirmationType.UsesOBOTokens())
                    await _eventDispatcherMetaService.DispatchEvent(AuthJanitorSystemEvents.RotationTaskCreatedForApproval, nameof(ScheduleRekeyingTasksService.CreateAndNotify), task);
                else
                    await _eventDispatcherMetaService.DispatchEvent(AuthJanitorSystemEvents.RotationTaskCreatedForAutomation, nameof(ScheduleRekeyingTasksService.CreateAndNotify), task);
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
            var secretsToRotate = await _managedSecrets.Get(s =>
                s.TaskConfirmationStrategies.HasFlag(taskConfirmationStrategies) &&
                s.Expiry < DateTimeOffset.UtcNow + TimeSpan.FromHours(leadTimeHours));

            var rekeyingTasks = await _rekeyingTasks.Get();
            return secretsToRotate
                        .Where(s => !rekeyingTasks.Any(t =>
                            t.ManagedSecretId == s.ObjectId &&
                            !t.RekeyingCompleted))
                        .ToList();
        }
    }
}