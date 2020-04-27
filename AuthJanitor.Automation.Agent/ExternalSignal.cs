// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Automation.Shared;
using AuthJanitor.Automation.Shared.DataStores;
using AuthJanitor.Automation.Shared.Models;
using AuthJanitor.Automation.Shared.ViewModels;
using AuthJanitor.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;

namespace AuthJanitor.Automation.Agent
{
    public class ExternalSignal : StorageIntegratedFunction
    {
        private const int RETURN_NO_CHANGE = 0;
        private const int RETURN_CHANGE_OCCURRED = 1;
        private const int RETURN_RETRY_SHORTLY = 2;

        private const int MAX_EXECUTION_SECONDS_BEFORE_RETRY = 30;

        private readonly AuthJanitorServiceConfiguration _serviceConfiguration;
        private readonly TaskExecutionManager _taskExecutionManager;
        private readonly EventDispatcherService _eventDispatcher;

        public ExternalSignal(
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

        [FunctionName("ExternalSignal")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "secrets/{managedSecretId:guid}/{nonce}")] HttpRequest req,
            Guid managedSecretId,
            string nonce,
            ILogger log)
        {
            _ = req; // unused but required for attribute

            log.LogInformation("External signal called to check ManagedSecret ID {0} against nonce {1}", managedSecretId, nonce);

            var secret = await ManagedSecrets.GetAsync(managedSecretId);
            if (secret == null)
                return new BadRequestErrorMessageResult("Invalid ManagedSecret ID");
            if (!secret.TaskConfirmationStrategies.HasFlag(TaskConfirmationStrategies.ExternalSignal))
                return new BadRequestErrorMessageResult("This ManagedSecret cannot be used with External Signals");

            if ((await RekeyingTasks.GetAsync(t => t.ManagedSecretId == secret.ObjectId))
                                    .Any(t => t.RekeyingInProgress))
            {
                return new OkObjectResult(RETURN_RETRY_SHORTLY);
            }

            if ((secret.IsValid && secret.TimeRemaining <= TimeSpan.FromHours(_serviceConfiguration.ExternalSignalRekeyableLeadTimeHours)) || !secret.IsValid)
            {
                var rekeyingTask = new Task(async () =>
                    {
                        var task = new RekeyingTask()
                        {
                            ManagedSecretId = secret.ObjectId,
                            Expiry = secret.Expiry,
                            Queued = DateTimeOffset.UtcNow,
                            RekeyingInProgress = true
                        };
                        await RekeyingTasks.CreateAsync(task);

                        await _taskExecutionManager.ExecuteRekeyingTaskWorkflow(task.ObjectId);
                    },
                    TaskCreationOptions.LongRunning);
                rekeyingTask.Start();

                if (!rekeyingTask.Wait(TimeSpan.FromSeconds(MAX_EXECUTION_SECONDS_BEFORE_RETRY)))
                {
                    log.LogInformation("Rekeying workflow was started but exceeded the maximum request time! ({0})", TimeSpan.FromSeconds(MAX_EXECUTION_SECONDS_BEFORE_RETRY));
                    return new OkObjectResult(RETURN_RETRY_SHORTLY);
                }
                else
                {
                    log.LogInformation("Completed rekeying workflow within maximum time! ({0})", TimeSpan.FromSeconds(MAX_EXECUTION_SECONDS_BEFORE_RETRY));
                    return new OkObjectResult(RETURN_CHANGE_OCCURRED);
                }
            }
            return new OkObjectResult(RETURN_NO_CHANGE);
        }
    }
}
