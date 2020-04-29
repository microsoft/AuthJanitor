// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Automation.Shared.Models;
using AuthJanitor.Providers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AuthJanitor.Automation.Shared
{
    public class TaskExecutionService
    {
        private readonly IDataStore<RekeyingTask> _rekeyingTasks;
        private readonly IDataStore<ManagedSecret> _managedSecrets;
        private readonly IDataStore<Resource> _resources;
        private readonly ProviderManagerService _providerManager;
        private readonly EventDispatcherService _eventDispatcher;
        private readonly CredentialProviderService _credentialProvider;

        public TaskExecutionService(
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
            try
            {
                credential = await _credentialProvider.GetAccessTokenAsync(task);
                if (credential == null)
                    throw new NotSupportedException("Access Token not found");
            }
            catch (Exception ex)
            {
                log.LogCritical(ex, "Exception retrieving Access Token");
                log.OuterException = GetExceptionSerialized(new Exception("Exception retrieving Access Token", ex));
            }

            log.UserDisplayName = credential.Username;
            log.UserEmail = credential.Username;

            if (task.ConfirmationType == TaskConfirmationStrategies.AdminCachesSignOff)
                log.UserDisplayName = task.PersistedCredentialUser;

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
                        await _credentialProvider.DestroyCachedAccessTokenAsync(task.PersistedCredentialId);
                    }

                    log.LogInformation("Completed rekeying workflow for ManagedSecret '{0}' (ID {1})", secret.Name, secret.ObjectId);
                    log.LogInformation("Rekeying task completed");

                    await _rekeyingTasks.UpdateAsync(task);

                    if (task.ConfirmationType.UsesOBOTokens())
                        await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.RotationTaskCompletedManually, nameof(TaskExecutionService.ExecuteRekeyingTaskWorkflow), task);
                    else
                        await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.RotationTaskCompletedAutomatically, nameof(TaskExecutionService.ExecuteRekeyingTaskWorkflow), task);
                }
                else
                    await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.RotationTaskAttemptFailed, nameof(TaskExecutionService.ExecuteRekeyingTaskWorkflow), task);
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