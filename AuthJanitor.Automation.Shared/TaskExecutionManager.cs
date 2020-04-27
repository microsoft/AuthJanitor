// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Automation.Shared.DataStores;
using AuthJanitor.Automation.Shared.Models;
using AuthJanitor.Providers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AuthJanitor.Automation.Shared
{
    public class TaskExecutionManager
    {
        private readonly IDataStore<RekeyingTask> _rekeyingTasks;
        private readonly IDataStore<ManagedSecret> _managedSecrets;
        private readonly IDataStore<Resource> _resources;
        private readonly ProviderManagerService _providerManager;
        private readonly EventDispatcherService _eventDispatcher;
        private readonly CredentialProviderService _credentialProvider;

        public TaskExecutionManager(
            ProviderManagerService providerManager,
            EventDispatcherService eventDispatcher,
            CredentialProviderService credentialProvider,
            IDataStore<RekeyingTask> rekeyingTasks,
            IDataStore<ManagedSecret> managedSecrets,
            IDataStore<Resource> resources)
        {
            _providerManager = providerManager;
            _eventDispatcher = eventDispatcher;
            _credentialProvider = credentialProvider;
            _rekeyingTasks = rekeyingTasks;
            _resources = resources;
            _managedSecrets = managedSecrets;
        }

        public async Task ExecuteRekeyingTaskWorkflow(Guid taskId)
        {
            var log = new RekeyingAttemptLogger();
            var task = await _rekeyingTasks.GetAsync(taskId);
            task.RekeyingInProgress = true;
            await _rekeyingTasks.UpdateAsync(task);

            AccessTokenCredential credential = null;
            if (task.ConfirmationType == TaskConfirmationStrategies.AdminCachesSignOff)
            {
                if (task.PersistedCredentialId == default)
                {
                    log.LogError("Cached sign-off is preferred but no credentials were persisted!");
                    log.OuterException = GetExceptionSerialized(new Exception("Cached sign-off is preferred but no credentials were persisted!"));
                    return;
                }
                credential = await _credentialProvider.GetCachedIdentity(task.PersistedCredentialId);

                log.UserDisplayName = task.PersistedCredentialUser;
                log.UserEmail = credential.Username;
            }
            else if (task.ConfirmationType == TaskConfirmationStrategies.AdminSignsOffJustInTime)
            {
                credential = _credentialProvider.GetUserIdentity();
                log.UserDisplayName = credential.Username;
                log.UserEmail = credential.Username;
            }
            else if (task.ConfirmationType.UsesServicePrincipal())
            {
                credential = _credentialProvider.GetAgentIdentity();
                log.UserDisplayName = "AGENT.IDENTITY";
                log.UserEmail = "nop@agent.identity";
            }

            if (credential == null)
            {
                log.LogError("No credentials were able to be loaded for this operation!");
                log.OuterException = GetExceptionSerialized(new Exception("No credentials were able to be loaded for this operation!"));
                return;
            }

            var secret = await _managedSecrets.GetAsync(task.ManagedSecretId);
            log.LogInformation("Beginning rekeying of secret ID {0}", task.ManagedSecretId);

            var resources = await _resources.GetAsync(r => secret.ResourceIds.Contains(r.ObjectId));

            try
            {
                var providers = resources.Select(r => _providerManager.GetProviderInstance(r.ProviderType, r.ProviderConfiguration));
                await _providerManager.ExecuteRekeyingWorkflow(log, secret.ValidPeriod, providers.ToArray());

                task.RekeyingInProgress = false;
                task.RekeyingCompleted = log.IsSuccessfulAttempt;
                task.RekeyingFailed = !log.IsSuccessfulAttempt;
                task.Attempts.Add(log);

                await _rekeyingTasks.UpdateAsync(task);

                if (task.RekeyingCompleted)
                {
                    secret.LastChanged = DateTimeOffset.UtcNow;
                    await _managedSecrets.UpdateAsync(secret);

                    if (task.PersistedCredentialId != default)
                    {
                        log.LogInformation("Destroying persisted credential");
                        await _credentialProvider.DestroyCachedIdentity(task.PersistedCredentialId);
                    }

                    log.LogInformation("Completed rekeying workflow for ManagedSecret '{0}' (ID {1})", secret.Name, secret.ObjectId);
                    log.LogInformation("Rekeying task completed");

                    await _rekeyingTasks.UpdateAsync(task);

                    if (task.ConfirmationType.UsesOBOTokens())
                        await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.RotationTaskCompletedManually, nameof(TaskExecutionManager.ExecuteRekeyingTaskWorkflow), task);
                    else
                        await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.RotationTaskCompletedAutomatically, nameof(TaskExecutionManager.ExecuteRekeyingTaskWorkflow), task);
                }
                else
                    await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.RotationTaskAttemptFailed, nameof(TaskExecutionManager.ExecuteRekeyingTaskWorkflow), task);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error executing Rekeying workflow!");
                log.OuterException = GetExceptionSerialized(ex);
                return;
            }
        }

        private string GetExceptionSerialized(Exception ex) =>
            JsonConvert.SerializeObject(ex, Formatting.Indented);
    }
}
