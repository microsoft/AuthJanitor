// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Automation.Shared;
using AuthJanitor.Automation.Shared.Models;
using AuthJanitor.Automation.Shared.ViewModels;
using AuthJanitor.Providers;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace AuthJanitor.Automation.Agent
{
    public class PerformAutoRekeyingTasks : StorageIntegratedFunction
    {
        private readonly AuthJanitorServiceConfiguration _serviceConfiguration;
        private readonly TaskExecutionManager _taskExecutionManager;
        private readonly EventDispatcherService _eventDispatcher;

        public PerformAutoRekeyingTasks(
            AuthJanitorServiceConfiguration serviceConfiguration,
            EventDispatcherService eventDispatcher,
            TaskExecutionManager taskExecutionManager,
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
            _eventDispatcher = eventDispatcher;
            _taskExecutionManager = taskExecutionManager;
        }

        [FunctionName("PerformAutoRekeyingTasks")]
        public async Task Run([TimerTrigger("0 */2 * * * *")]TimerInfo myTimer, ILogger log)
        {
            _ = myTimer; // unused but required for attribute

            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            var toRekey = await RekeyingTasks.GetAsync(t =>
                (t.ConfirmationType == TaskConfirmationStrategies.AdminCachesSignOff ||
                 t.ConfirmationType == TaskConfirmationStrategies.AutomaticRekeyingAsNeeded ||
                 t.ConfirmationType == TaskConfirmationStrategies.AutomaticRekeyingScheduled) &&
                DateTimeOffset.UtcNow + TimeSpan.FromHours(_serviceConfiguration.AutomaticRekeyableJustInTimeLeadTimeHours) > t.Expiry);

            foreach (var task in toRekey)
            {
                await _taskExecutionManager.ExecuteRekeyingTaskWorkflow(task.ObjectId);
            }
        }
    }
}
