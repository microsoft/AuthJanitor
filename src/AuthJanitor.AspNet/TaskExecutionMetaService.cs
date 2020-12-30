// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.UI.Shared.Models;
using AuthJanitor.EventSinks;
using AuthJanitor.IdentityServices;
using AuthJanitor.Providers;
using AuthJanitor.SecureStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AuthJanitor.DataStores;

namespace AuthJanitor
{
    public class TaskExecutionMetaService
    {
        private readonly IDataStore<ManagedSecret> _managedSecrets;
        private readonly IDataStore<RekeyingTask> _rekeyingTasks;
        private readonly IDataStore<Resource> _resources;
        private readonly ISecureStorage _secureStorageProvider;

        private readonly ILogger<TaskExecutionMetaService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ProviderManagerService _providerManagerService;

        private readonly EventDispatcherService _eventDispatcherService;
        private readonly IIdentityService _identityService;

        private readonly AuthJanitorService _authJanitorService;

        public TaskExecutionMetaService(
            ILogger<TaskExecutionMetaService> logger,
            IServiceProvider serviceProvider,
            EventDispatcherService eventDispatcherService,
            IIdentityService identityService,
            ProviderManagerService providerManagerService,
            IDataStore<ManagedSecret> managedSecrets,
            IDataStore<RekeyingTask> rekeyingTasks,
            IDataStore<Resource> resources,
            ISecureStorage secureStorageProvider,
            AuthJanitorService authJanitorService)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _eventDispatcherService = eventDispatcherService;
            _identityService = identityService;
            _providerManagerService = providerManagerService;
            _managedSecrets = managedSecrets;
            _rekeyingTasks = rekeyingTasks;
            _resources = resources;
            _secureStorageProvider = secureStorageProvider;
            _authJanitorService = authJanitorService;
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
                await _eventDispatcherService.DispatchEvent(AuthJanitorSystemEvents.RotationTaskAttemptFailed, nameof(TaskExecutionMetaService.ExecuteTask), task);
                throw ex;
            }
        }

        private TProvider DuplicateProvider<TProvider>(TProvider provider)
            where TProvider : IAuthJanitorProvider => _providerManagerService.GetProviderInstance(provider);

        public async Task ExecuteTask(Guid taskId, CancellationToken cancellationToken)
        {
            // Prepare record
            _logger.LogInformation("Retrieving task {TaskId}", taskId);
            var task = await _rekeyingTasks.GetOne(taskId, cancellationToken);
            task.RekeyingInProgress = true;

            // Create task to perform regular updates to UI (every 15s)
            _logger.LogInformation("Starting log update task");
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
            _logger.LogInformation("Retrieving resources for secret {SecretId}", secret.ObjectId);
            var resources = await _resources.Get(r => secret.ResourceIds.Contains(r.ObjectId), cancellationToken);

            var workflowCollection = await _authJanitorService.ExecuteAsync(
                secret.ValidPeriod,
                async (pwac) =>
                {
                    if (!task.Attempts.Any(a => a.StartedExecution == pwac.StartedExecution))
                    {
                        task.Attempts.Add(pwac);
                        await _rekeyingTasks.Update(task, cancellationToken);
                    }
                },
                resources.Select(r =>
                {
                    TokenSources tokenSource = TokenSources.Unknown;
                    string tokenParameter = string.Empty;
                    switch (secret.TaskConfirmationStrategies)
                    {
                        case TaskConfirmationStrategies.AdminSignsOffJustInTime:
                            tokenSource = TokenSources.OBO;
                            break;
                        case TaskConfirmationStrategies.AdminCachesSignOff:
                            tokenSource = TokenSources.Persisted;
                            tokenParameter = task.PersistedCredentialId.ToString();
                            break;
                        case TaskConfirmationStrategies.AutomaticRekeyingAsNeeded:
                        case TaskConfirmationStrategies.AutomaticRekeyingScheduled:
                        case TaskConfirmationStrategies.ExternalSignal:
                            tokenSource = TokenSources.ServicePrincipal;
                            break;
                    }
                    return new ProviderExecutionParameters()
                    {
                        ProviderType = r.ProviderType,
                        ProviderConfiguration = r.ProviderConfiguration,
                        AgentId = secret.ExecutingAgentId,
                        TokenSource = tokenSource,
                        TokenParameter = tokenParameter
                    };
                }).ToArray());

            // Update Task record
            _logger.LogInformation("Completing task record");
            task.RekeyingInProgress = false;
            task.RekeyingCompleted = (workflowCollection?.HasBeenExecuted).GetValueOrDefault();
            task.RekeyingCompleted = (workflowCollection?.HasBeenExecutedSuccessfully).GetValueOrDefault();
            await _rekeyingTasks.Update(task, cancellationToken);

            if (workflowCollection.HasBeenExecutedSuccessfully)
            {
                if (task.ConfirmationType.UsesOBOTokens())
                    await _eventDispatcherService.DispatchEvent(AuthJanitorSystemEvents.RotationTaskCompletedManually, nameof(TaskExecutionMetaService.ExecuteTask), task);
                else
                    await _eventDispatcherService.DispatchEvent(AuthJanitorSystemEvents.RotationTaskCompletedAutomatically, nameof(TaskExecutionMetaService.ExecuteTask), task);
            }
            else
                await _eventDispatcherService.DispatchEvent(AuthJanitorSystemEvents.RotationTaskAttemptFailed, nameof(TaskExecutionMetaService.ExecuteTask), task);
        }
    }
}
