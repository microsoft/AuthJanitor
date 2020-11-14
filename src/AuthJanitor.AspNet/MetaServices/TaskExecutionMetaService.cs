// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.UI.Shared.Models;
using AuthJanitor.EventSinks;
using AuthJanitor.IdentityServices;
using AuthJanitor.Integrations.DataStores;
using AuthJanitor.Providers;
using AuthJanitor.SecureStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AuthJanitor.UI.Shared.MetaServices
{
    public class TaskExecutionMetaService
    {
        private readonly IDataStore<ManagedSecret> _managedSecrets;
        private readonly IDataStore<RekeyingTask> _rekeyingTasks;
        private readonly IDataStore<Resource> _resources;
        private readonly ISecureStorage _secureStorageProvider;

        private readonly IServiceProvider _serviceProvider;
        private readonly ProviderManagerService _providerManagerService;

        private readonly EventDispatcherMetaService _eventDispatcherMetaService;
        private readonly IIdentityService _identityService;

        public TaskExecutionMetaService(
            IServiceProvider serviceProvider,
            EventDispatcherMetaService eventDispatcherMetaService,
            IIdentityService identityService,
            ProviderManagerService providerManagerService,
            IDataStore<ManagedSecret> managedSecrets,
            IDataStore<RekeyingTask> rekeyingTasks,
            IDataStore<Resource> resources,
            ISecureStorage secureStorageProvider)
        {
            _serviceProvider = serviceProvider;
            _eventDispatcherMetaService = eventDispatcherMetaService;
            _identityService = identityService;
            _providerManagerService = providerManagerService;
            _managedSecrets = managedSecrets;
            _rekeyingTasks = rekeyingTasks;
            _resources = resources;
            _secureStorageProvider = secureStorageProvider;
        }

        public async Task CacheBackCredentialsForTaskIdAsync(Guid taskId, CancellationToken cancellationToken)
        {
            var task = await _rekeyingTasks.GetOne(taskId, cancellationToken);
            if (task == null)
                throw new KeyNotFoundException("Task not found");

            if (task.ConfirmationType != TaskConfirmationStrategies.AdminCachesSignOff)
                throw new InvalidOperationException("Task does not persist credentials");

            if (_secureStorageProvider == null)
                throw new NotSupportedException("Must register an ISecureStorageProvider");

            var credentialId = await _identityService.GetAccessTokenOnBehalfOfCurrentUserAsync()
                                       .ContinueWith(t => _secureStorageProvider.Persist(task.Expiry, t.Result))
                                       .Unwrap();

            task.PersistedCredentialId = credentialId;
            task.PersistedCredentialUser = _identityService.UserName;

            await _rekeyingTasks.Update(task, cancellationToken);
        }

        public async Task<AccessTokenCredential> GetTokenCredentialAsync(Guid taskId, CancellationToken cancellationToken)
        {
            var task = await _rekeyingTasks.GetOne(taskId, cancellationToken);

            // Retrieve credentials for Task
            AccessTokenCredential credential = null;
            try
            {
                if (task.ConfirmationType == TaskConfirmationStrategies.AdminCachesSignOff)
                {
                    if (task.PersistedCredentialId == default)
                        throw new KeyNotFoundException("Cached sign-off is preferred but no credentials were persisted!");

                    if (_secureStorageProvider == null)
                        throw new NotSupportedException("Must register an ISecureStorageProvider");

                    credential = await _secureStorageProvider.Retrieve<AccessTokenCredential>(task.PersistedCredentialId);
                }
                else if (task.ConfirmationType == TaskConfirmationStrategies.AdminSignsOffJustInTime)
                    credential = await _identityService.GetAccessTokenOnBehalfOfCurrentUserAsync();
                else if (task.ConfirmationType.UsesServicePrincipal())
                    credential = await _identityService.GetAccessTokenForApplicationAsync();
                else
                    throw new NotSupportedException("No Access Tokens could be generated for this Task!");

                if (credential == null || string.IsNullOrEmpty(credential.AccessToken))
                    throw new InvalidOperationException("Access Token was found, but was blank or invalid");

                credential.DisplayUserName = credential.Username;
                credential.DisplayEmail = credential.Username;
                if (task.ConfirmationType.UsesOBOTokens())
                {
                    if (!string.IsNullOrEmpty(task.PersistedCredentialUser))
                        credential.DisplayUserName = task.PersistedCredentialUser;
                    else
                    {
                        credential.DisplayUserName = _identityService.UserName;
                        credential.DisplayEmail = _identityService.UserEmail;
                    }
                }

                return credential;
            }
            catch (Exception ex)
            {
                await _eventDispatcherMetaService.DispatchEvent(AuthJanitorSystemEvents.RotationTaskAttemptFailed, nameof(TaskExecutionMetaService.ExecuteTask), task);
                throw ex;
            }
        }

        private TProvider DuplicateProvider<TProvider>(TProvider provider)
            where TProvider : IAuthJanitorProvider => _providerManagerService.GetProviderInstance(provider);

        public async Task ExecuteTask(Guid taskId, CancellationToken cancellationToken)
        {
            // Prepare record
            var task = await _rekeyingTasks.GetOne(taskId, cancellationToken);
            task.RekeyingInProgress = true;

            // Create task to perform regular updates to UI (every 15s)
            var logUpdateCancellationTokenSource = new CancellationTokenSource();
            var logUpdateTask = Task.Run(async () =>
            {
                while (task.RekeyingInProgress)
                {
                    await Task.Delay(15 * 1000);
                    await _rekeyingTasks.Update(task, cancellationToken);
                }
            }, logUpdateCancellationTokenSource.Token);

            // Retrieve the secret configuration and its resources
            var secret = await _managedSecrets.GetOne(task.ManagedSecretId, cancellationToken);
            var resources = await _resources.Get(r => secret.ResourceIds.Contains(r.ObjectId), cancellationToken);

            ProviderWorkflowActionCollection workflowCollection = null;
            try
            {
                // Create configured providers for each resource
                var providers = resources.Select(r => _providerManagerService.GetProviderInstance(
                    r.ProviderType,
                    r.ProviderConfiguration)).ToList();

                // Generate a workflow collection from the configured providers
                workflowCollection = _providerManagerService.CreateWorkflowCollection(
                    secret.ValidPeriod,
                    providers);

                // Get the token credential for this task and embed it
                workflowCollection.EmbedCredentials(await GetTokenCredentialAsync(taskId, cancellationToken));

                // TODO: Per-action credentialing will go here eventually
                // For now, use the snippet above for all instances
                //workflowCollection.Actions.ToList().ForEach(a =>
                //{
                //    a.Instance.Credential = credential;
                //});

                // Update the UI with the data from this attempt
                task.Attempts.Add(workflowCollection);
                await _rekeyingTasks.Update(task, cancellationToken);

                // Execute the workflow collection
                try { await workflowCollection.Run(); }
                catch (Exception)
                {
                    await _eventDispatcherMetaService.DispatchEvent(AuthJanitorSystemEvents.RotationTaskAttemptFailed, nameof(TaskExecutionMetaService.ExecuteTask), task);
                }
            }
            catch (Exception)
            {
                await _eventDispatcherMetaService.DispatchEvent(AuthJanitorSystemEvents.RotationTaskAttemptFailed, nameof(TaskExecutionMetaService.ExecuteTask), task);
            }

            // Update Task record
            task.RekeyingInProgress = false;
            task.RekeyingCompleted = (workflowCollection?.HasBeenExecuted).GetValueOrDefault();
            task.RekeyingCompleted = (workflowCollection?.HasBeenExecutedSuccessfully).GetValueOrDefault();

            // End the regular update task and perform one final data update with the results
            logUpdateCancellationTokenSource.Cancel();
            await _rekeyingTasks.Update(task, cancellationToken);

            // Run cleanup if Task is complete
            if (!task.RekeyingFailed)
            {
                try
                {
                    secret.LastChanged = DateTimeOffset.UtcNow;
                    if (task.PersistedCredentialId != default && task.PersistedCredentialId != Guid.Empty)
                    {
                        await _secureStorageProvider.Destroy(task.PersistedCredentialId);
                        task.PersistedCredentialId = default;
                        task.PersistedCredentialUser = default;
                    }

                    await _rekeyingTasks.Update(task, cancellationToken);
                }
                catch (Exception)
                {
                    await _eventDispatcherMetaService.DispatchEvent(AuthJanitorSystemEvents.AnomalousEventOccurred, nameof(TaskExecutionMetaService.ExecuteTask),
                        "Failure to clean up persisted credentials");
                }


                if (task.ConfirmationType.UsesOBOTokens())
                    await _eventDispatcherMetaService.DispatchEvent(AuthJanitorSystemEvents.RotationTaskCompletedManually, nameof(TaskExecutionMetaService.ExecuteTask), task);
                else
                    await _eventDispatcherMetaService.DispatchEvent(AuthJanitorSystemEvents.RotationTaskCompletedAutomatically, nameof(TaskExecutionMetaService.ExecuteTask), task);
            }
            else
                await _eventDispatcherMetaService.DispatchEvent(AuthJanitorSystemEvents.RotationTaskAttemptFailed, nameof(TaskExecutionMetaService.ExecuteTask), task);
        }
    }
}
