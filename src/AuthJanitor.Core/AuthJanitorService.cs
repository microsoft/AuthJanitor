// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Agents;
using AuthJanitor.CryptographicImplementations;
using AuthJanitor.EventSinks;
using AuthJanitor.IdentityServices;
using AuthJanitor.Integrity;
using AuthJanitor.Providers;
using AuthJanitor.SecureStorage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AuthJanitor
{
    public class AuthJanitorServiceOptions
    {
        public string InstanceId { get; set; }
    }

    public static class AuthJanitorServiceExtensions
    {
        public static void AddAuthJanitorService(this IServiceCollection serviceCollection,
            string instanceIdentity,
            Type[] providerTypes)
        {
            serviceCollection.AddSingleton<EventDispatcherService>();
            serviceCollection.AddSingleton<SystemIntegrityService>();

            serviceCollection.AddSingleton<ProviderManagerService>((s) => 
                new ProviderManagerService(s, providerTypes));

            serviceCollection.Configure<AuthJanitorServiceOptions>((o) => o.InstanceId = instanceIdentity);
            serviceCollection.AddSingleton<AuthJanitorService>();

            serviceCollection.AddTransient<ProviderWorkflowActionLogger>();
            serviceCollection.AddTransient(typeof(ProviderWorkflowActionLogger<>), typeof(ProviderWorkflowActionLogger<>));
        }
    }

    public class AuthJanitorService
    {
        private readonly ILogger<AuthJanitorService> _logger;
        private readonly ProviderManagerService _providerManagerService;
        private readonly ICryptographicImplementation _cryptographicImplementation;
        private readonly ISecureStorage _secureStorage;
        private readonly IIdentityService _identityService;
        private readonly EventDispatcherService _eventDispatcher;
        private readonly IAgentCommunicationProvider _agentCommunicationProvider;
        private readonly IOptions<AuthJanitorServiceOptions> _options;

        public AuthJanitorService(
            ILogger<AuthJanitorService> logger,
            ProviderManagerService providerManagerService,
            ICryptographicImplementation cryptographicImplementation,
            ISecureStorage secureStorage,
            IIdentityService identityService,
            EventDispatcherService eventDispatcherService,
            IAgentCommunicationProvider agentCommunicationProvider,
            IOptions<AuthJanitorServiceOptions> options)
        {
            _logger = logger;
            _providerManagerService = providerManagerService;
            _cryptographicImplementation = cryptographicImplementation;
            _secureStorage = secureStorage;
            _identityService = identityService;
            _eventDispatcher = eventDispatcherService;
            _agentCommunicationProvider = agentCommunicationProvider;
            _options = options;
        }

        public async Task DispatchOrExecuteAsync(
            TimeSpan secretValidPeriod,
            Func<ProviderWorkflowActionCollection, Task> periodicUpdateFunction,
            string dispatchTopicState,
            params ProviderExecutionParameters[] providerConfigurations)
        {
            if (providerConfigurations.Any(c => c.AgentId != _options.Value.InstanceId))
            {
                var agentId = providerConfigurations.FirstOrDefault(c => c.AgentId != _options.Value.InstanceId)?.AgentId;
                await _agentCommunicationProvider.Send(
                    await AgentMessageEnvelope.Create(_cryptographicImplementation,
                        _options.Value.InstanceId,
                        agentId,
                        new AgentProviderCommandMessage()
                        {
                            State = dispatchTopicState,
                            ValidPeriod = secretValidPeriod,
                            Providers = providerConfigurations.Where(c => c.AgentId == agentId).ToList()
                        }));
            }

            if (providerConfigurations.Any(c => c.AgentId == _options.Value.InstanceId))
            {
                await ExecuteAsync(secretValidPeriod,
                    periodicUpdateFunction,
                    providerConfigurations.Where(c => c.AgentId == _options.Value.InstanceId).ToArray());
            }
        }

        public async Task ProcessMessageAsync(
            string serializedMessage,
            Func<ProviderWorkflowActionCollection, Task> periodicUpdateFunction)
        {
            var envelope = JsonConvert.DeserializeObject<AgentMessageEnvelope>(serializedMessage);
            envelope.MessageObject = null;

            if (envelope.Target != _options.Value.InstanceId)
                return;

            if (!await envelope.VerifyAndUnpack(_cryptographicImplementation))
            {
                await _eventDispatcher.DispatchEvent(
                    EventSinks.AuthJanitorSystemEvents.AnomalousEventOccurred,
                    nameof(AuthJanitorService.ProcessMessageAsync),
                    "Failed to verify agent message! This may indicate agent compromise.");
                throw new Exception("Message verification failed!");
            }

            if (envelope.MessageObject is AgentProviderCommandMessage)
            {
                var message = envelope.MessageObject as AgentProviderCommandMessage;
                await ExecuteAsync(message.ValidPeriod, 
                    periodicUpdateFunction,
                    message.Providers.ToArray());
            }
            if (envelope.MessageObject is AgentProviderStatusMessage)
            {
                var message = envelope.MessageObject as AgentProviderStatusMessage;
                var taskId = Guid.Parse(message.State);
                await periodicUpdateFunction(message.WorkflowActionCollection);
            }
        }

        public async Task<string> CreateMessage(
            string destination,
            IAgentMessage message)
        {
            var envelope = await AgentMessageEnvelope.Create(_cryptographicImplementation,
                _options.Value.InstanceId,
                destination,
                message);
            envelope.MessageObject = null;
            return JsonConvert.SerializeObject(envelope);
        }

        public async Task<Guid> StashCredentialForCurrentUserAsync(DateTimeOffset expiry = default) =>
            await StashCredentialAsync(
                await _identityService.GetAccessTokenOnBehalfOfCurrentUserAsync(),
                expiry);

        public async Task<Guid> StashCredentialForCurrentAppAsync(DateTimeOffset expiry = default) =>
            await StashCredentialAsync(
                await _identityService.GetAccessTokenForApplicationAsync(),
                expiry);

        public async Task<Guid> StashCredentialAsync(
            AccessTokenCredential token,
            DateTimeOffset expiry = default)
        {
            if (expiry == default) expiry = DateTimeOffset.Now.AddDays(7);
            return await _secureStorage.Persist(expiry, token);
        }

        public async Task<ProviderWorkflowActionCollection> ExecuteAsync(
            TimeSpan secretValidPeriod,
            Func<ProviderWorkflowActionCollection, Task> periodicUpdateFunction,
            params ProviderExecutionParameters[] providerConfigurations)
        {
            ProviderWorkflowActionCollection workflowCollection =
                new ProviderWorkflowActionCollection();
            try
            {
                var persisted = new Dictionary<Guid, AccessTokenCredential>();
                if (providerConfigurations.Any(p => p.TokenSource == TokenSources.Persisted))
                {
                    _logger.LogInformation("Downloading persisted tokens");
                    foreach (var item in providerConfigurations.Where(p => p.TokenSource == TokenSources.Persisted))
                    {
                        var guid = Guid.Parse(item.TokenParameter);
                        persisted[guid] = await _secureStorage.Retrieve<AccessTokenCredential>(guid);
                    }
                }

                AccessTokenCredential obo = null, msi = null;
                if (providerConfigurations.Any(p => p.TokenSource == TokenSources.OBO))
                {
                    _logger.LogInformation("Acquiring OBO token");
                    obo = await _identityService.GetAccessTokenOnBehalfOfCurrentUserAsync();
                }
                if (providerConfigurations.Any(p => p.TokenSource == TokenSources.ServicePrincipal))
                {
                    _logger.LogInformation("Acquiring application token");
                    msi = await _identityService.GetAccessTokenForApplicationAsync();
                }

                _logger.LogInformation("Getting providers for {ResourceCount} resources", providerConfigurations.Count());
                await Task.WhenAll(providerConfigurations.Select(async r =>
                {
                    switch (r.TokenSource)
                    {
                        case TokenSources.Explicit:
                            r.AccessToken = JsonConvert.DeserializeObject<AccessTokenCredential>(r.TokenParameter);
                            break;
                        case TokenSources.OBO:
                            r.AccessToken = obo;
                            r.AccessToken.DisplayEmail = _identityService.UserEmail;
                            r.AccessToken.DisplayUserName = _identityService.UserName;
                            break;
                        case TokenSources.Persisted:
                            r.AccessToken = persisted[Guid.Parse(r.TokenParameter)];
                            r.AccessToken.DisplayEmail = r.AccessToken.Username;
                            r.AccessToken.DisplayUserName = r.AccessToken.Username;
                            break;
                        case TokenSources.ServicePrincipal:
                            r.AccessToken = msi;
                            break;
                        case TokenSources.Unknown:
                        default:
                            await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.AnomalousEventOccurred,
                                nameof(AuthJanitorService.ExecuteAsync),
                                $"TokenSource was unknown for a provider! ({r.ProviderType})");
                            break;
                    }
                    return r;
                }));

                // --- end access token acquisition/embed ---

                var providers = providerConfigurations.Select(r =>
                {
                    var p = _providerManagerService.GetProviderInstance(
                        r.ProviderType,
                        r.ProviderConfiguration);
                    if (r.AccessToken != null)
                        p.Credential = r.AccessToken;
                    return p;
                }).ToList();

                workflowCollection = _providerManagerService.CreateWorkflowCollection(
                    secretValidPeriod,
                    providers);

                _logger.LogInformation("Creating workflow execution task");
                Task workflowCollectionRunTask = new Task(async () =>
                {
                    try { await workflowCollection.Run(); }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing action(s)");
                        throw ex;
                    }
                });

                _logger.LogInformation("Creating tracker task for workflow collection");
                var logUpdateCancellationTokenSource = new CancellationTokenSource();
                var periodicUpdateTask = Task.Run(async () =>
                {
                    while (!workflowCollectionRunTask.IsCompleted &&
                           !workflowCollectionRunTask.IsCanceled)
                    {
                        await Task.Delay(5 * 1000);
                        await periodicUpdateFunction(workflowCollection);
                    }
                }, logUpdateCancellationTokenSource.Token);

                _logger.LogInformation("Executing {ActionCount} actions", workflowCollection.Actions.Count);
                await workflowCollectionRunTask;
                _logger.LogInformation("Execution complete", workflowCollection.Actions.Count);

                logUpdateCancellationTokenSource.Cancel();
                await periodicUpdateFunction(workflowCollection);

                if (workflowCollection.HasBeenExecutedSuccessfully)
                {
                    if (providerConfigurations.Any(p => p.TokenSource == TokenSources.Persisted))
                    {
                        _logger.LogInformation("Cleaning up persisted tokens");
                        foreach (var item in providerConfigurations.Where(p => p.TokenSource == TokenSources.Persisted))
                            await _secureStorage.Destroy(Guid.Parse(item.TokenParameter));
                    }
                }

                return workflowCollection;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preparing workflow: {ex}", ex);
                await _eventDispatcher.DispatchEvent(
                    AuthJanitorSystemEvents.RotationTaskAttemptFailed, 
                    nameof(AuthJanitorService.ExecuteAsync),
                    "Error executing provider workflow");

                return workflowCollection;
            }
        }
    }
}
