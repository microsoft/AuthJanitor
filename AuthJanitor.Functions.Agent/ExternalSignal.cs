// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.UI.Shared;
using AuthJanitor.UI.Shared.MetaServices;
using AuthJanitor.UI.Shared.Models;
using AuthJanitor.UI.Shared.ViewModels;
using AuthJanitor.Integrations.DataStores;
using AuthJanitor.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace AuthJanitor
{
    public class ExternalSignal
    {
        private const int RETURN_NO_CHANGE = 0;
        private const int RETURN_CHANGE_OCCURRED = 1;
        private const int RETURN_RETRY_SHORTLY = 2;

        private const int MAX_EXECUTION_SECONDS_BEFORE_RETRY = 30;

        private readonly AuthJanitorCoreConfiguration _configuration;
        private readonly TaskExecutionMetaService _taskExecutionMetaService;

        private readonly IDataStore<ManagedSecret> _managedSecrets;
        private readonly IDataStore<RekeyingTask> _rekeyingTasks;

        public ExternalSignal(
            IOptions<AuthJanitorCoreConfiguration> configuration,
            TaskExecutionMetaService taskExecutionMetaService,
            IDataStore<ManagedSecret> managedSecretStore,
            IDataStore<RekeyingTask> rekeyingTaskStore)
        {
            _configuration = configuration.Value;
            _taskExecutionMetaService = taskExecutionMetaService;

            _managedSecrets = managedSecretStore;
            _rekeyingTasks = rekeyingTaskStore;
        }

        [FunctionName("ExternalSignal")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "secrets/{managedSecretId:guid}/{nonce}")] HttpRequest req,
            Guid managedSecretId,
            string nonce,
            ILogger log)
        {
            _ = req; // unused but required for attribute

            log.LogInformation("External signal called to check ManagedSecret ID {ManagedSecretId} against nonce {Nonce}", managedSecretId, nonce);

            var secret = await _managedSecrets.GetOne(managedSecretId);
            if (secret == null)
                return new BadRequestErrorMessageResult("Invalid ManagedSecret ID");
            if (!secret.TaskConfirmationStrategies.HasFlag(TaskConfirmationStrategies.ExternalSignal))
                return new BadRequestErrorMessageResult("This ManagedSecret cannot be used with External Signals");

            if ((await _rekeyingTasks.Get(t => t.ManagedSecretId == secret.ObjectId))
                                    .Any(t => t.RekeyingInProgress))
            {
                return new OkObjectResult(RETURN_RETRY_SHORTLY);
            }

            if ((secret.IsValid && secret.TimeRemaining <= TimeSpan.FromHours(_configuration.ExternalSignalRekeyableLeadTimeHours)) || !secret.IsValid)
            {
                var executeRekeyingTask = Task.Run(async () =>
                    {
                        var rekeyingTask = new RekeyingTask()
                        {
                            ManagedSecretId = secret.ObjectId,
                            Expiry = secret.Expiry,
                            Queued = DateTimeOffset.UtcNow,
                            RekeyingInProgress = true
                        };
                        
                        await _rekeyingTasks.Create(rekeyingTask).ConfigureAwait(false);

                        await _taskExecutionMetaService.ExecuteTask(rekeyingTask.ObjectId).ConfigureAwait(false);
                    });
                
                var timeout = TimeSpan.FromSeconds(MAX_EXECUTION_SECONDS_BEFORE_RETRY);
                var timeoutCancellationTokenSource = new CancellationTokenSource();
                var timeoutTask = Task.Delay(timeout, timeoutCancellationTokenSource.Token);

                var completedTask = await Task.WhenAny(executeRekeyingTask, timeoutTask);

                // If the task that completed first was the timeout task we need to let the caller know it's still running
                if (completedTask == timeoutTask)
                {
                    log.LogInformation("Rekeying workflow was started but exceeded the maximum request time! ({MaxExecutionRequestTime})", timeout);
                    return new OkObjectResult(RETURN_RETRY_SHORTLY);
                }
                else
                {
                    // Signal that the timeout task can be canceled
                    timeoutCancellationTokenSource.Cancel();

                    // The rekeying task completed in time, let the caller know
                    log.LogInformation("Completed rekeying workflow within maximum time! ({MaxExecutionRequestTime})", timeout);
                    return new OkObjectResult(RETURN_CHANGE_OCCURRED);
                }
            }
            return new OkObjectResult(RETURN_NO_CHANGE);
        }
    }
}
