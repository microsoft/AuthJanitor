// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers;
using AuthJanitor.Providers.Capabilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AuthJanitor
{
    public class ProviderManagerService
    {
        public static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions()
        {
            WriteIndented = false,
            IgnoreNullValues = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly ILogger _logger;
        private readonly IServiceProvider _serviceProvider;

        public ProviderManagerService(
            IServiceProvider serviceProvider,
            params Type[] providerTypes)
        {
            _logger = serviceProvider.GetRequiredService<ILogger<ProviderManagerService>>();
            _serviceProvider = serviceProvider;
            LoadedProviders = providerTypes
                .Where(type => !type.IsAbstract && typeof(IAuthJanitorProvider).IsAssignableFrom(type))
                .Select(type => new LoadedProviderMetadata()
                {
                    OriginatingFile = Path.GetFileName(type.Assembly.Location),
                    AssemblyName = type.Assembly.GetName(),
                    ProviderTypeName = type.AssemblyQualifiedName,
                    ProviderType = type,
                    ProviderConfigurationType = type.BaseType.GetGenericArguments()[0],
                    Details = type.GetCustomAttribute<ProviderAttribute>()
                })
                .ToList()
                .AsReadOnly();
        }

        public bool HasProvider(string providerName) => LoadedProviders.Any(p => p.ProviderTypeName == providerName);

        public LoadedProviderMetadata GetProviderMetadata(string providerName)
        {
            if (!HasProvider(providerName))
                throw new Exception($"Provider '{providerName}' not available!");
            else
                return LoadedProviders.First(p => p.ProviderTypeName == providerName);
        }

        public IAuthJanitorProvider GetProviderInstance(string providerName)
        {
            var metadata = GetProviderMetadata(providerName);
            return ActivatorUtilities.CreateInstance(_serviceProvider, metadata.ProviderType) as IAuthJanitorProvider;
        }

        public IAuthJanitorProvider GetProviderInstanceDefault(string providerName)
        {
            var instance = GetProviderInstance(providerName);
            instance.SerializedConfiguration = GetProviderConfiguration(providerName, GetProviderConfiguration(providerName));
            return instance;
        }

        public IAuthJanitorProvider GetProviderInstance(string providerName, string serializedProviderConfiguration)
        {
            var instance = GetProviderInstance(providerName);
            instance.SerializedConfiguration = serializedProviderConfiguration;
            return instance;
        }

        public TProvider GetProviderInstance<TProvider>(TProvider existingProviderToClone)
            where TProvider : IAuthJanitorProvider =>
            (TProvider)GetProviderInstance(existingProviderToClone.GetType().AssemblyQualifiedName, existingProviderToClone.SerializedConfiguration);

        public AuthJanitorProviderConfiguration GetProviderConfiguration(string name) => ActivatorUtilities.CreateInstance(_serviceProvider, GetProviderMetadata(name).ProviderConfigurationType) as AuthJanitorProviderConfiguration;
        public AuthJanitorProviderConfiguration GetProviderConfiguration(string name, string serializedConfiguration) => JsonSerializer.Deserialize(serializedConfiguration, GetProviderMetadata(name).ProviderConfigurationType, SerializerOptions) as AuthJanitorProviderConfiguration;
        public string GetProviderConfiguration<T>(string name, T configuration) => JsonSerializer.Serialize(configuration, GetProviderMetadata(name).ProviderConfigurationType, SerializerOptions);

        public bool TestProviderConfiguration(string name, string serializedConfiguration)
        {
            try { return GetProviderConfiguration(name, serializedConfiguration) != null; }
            catch { return false; }
        }

        public async Task<IEnumerable<ProviderResourceSuggestion>> EnumerateProviders(AccessTokenCredential credential)
        {
            var providers = (await Task.WhenAll(
                LoadedProviders.Select(p => GetProviderInstanceDefault(p.ProviderTypeName))
                    .OfType<ICanEnumerateResourceCandidates>()
                    .Select(p => EnumerateProviders(credential, p))))
                .Where(c => c != null)
                .SelectMany(c => c);

            foreach (var provider in providers.Where(p => p.AddressableNames.Any()))
            {
                foreach (var name in provider.AddressableNames)
                {
                    var refs = providers.Where(p => p.ResourceValues.Any(r => r.Contains(name)));
                    provider.ResourcesAddressingThis.AddRange(refs);
                }
            }

            return providers;
        }

        public async Task<IEnumerable<ProviderResourceSuggestion>> EnumerateProviders(AccessTokenCredential credential, IAuthJanitorProvider provider)
        {
            provider.Credential = credential;
            if (provider is ICanEnumerateResourceCandidates)
            {
                var enumerable = provider as ICanEnumerateResourceCandidates;
                try
                {
                    var results = await enumerable.EnumerateResourceCandidates(GetProviderConfiguration(provider.GetType().AssemblyQualifiedName, provider.SerializedConfiguration));
                    return results.Select(r => new ProviderResourceSuggestion()
                    { 
                        Name = r.Name,
                        ProviderType = r.ProviderType,
                        Configuration = r.Configuration,
                        SerializedConfiguration = JsonSerializer.Serialize<object>(r.Configuration),
                        AddressableNames = r.AddressableNames.Distinct(),
                        ResourceValues = r.ResourceValues.Distinct(),
                        ResourcesAddressingThis = r.ResourcesAddressingThis
                    });
                }
                catch (Exception ex)
                {
                    _serviceProvider.GetRequiredService<ILogger<ProviderManagerService>>().LogError(ex, "Error enumerating resource candidates for provider type " + provider.GetType().AssemblyQualifiedName);
                }
            }
            return new ProviderResourceSuggestion[0];
        }

        public IReadOnlyList<LoadedProviderMetadata> LoadedProviders { get; }

        private TProvider DuplicateProvider<TProvider>(TProvider provider)
            where TProvider : IAuthJanitorProvider =>
            _serviceProvider.GetRequiredService<ProviderManagerService>()
                            .GetProviderInstance(provider);

        public ProviderWorkflowActionCollection CreateWorkflowCollection(
            TimeSpan validPeriod,
            IEnumerable<IAuthJanitorProvider> providers)
        {
            var workflowCollection = new ProviderWorkflowActionCollection(_serviceProvider);

            workflowCollection.AddWithOneIncrement(providers
                .OfType<ICanRunSanityTests>()
                .Select(DuplicateProvider)
                .Select(p => ProviderWorkflowAction.Create(
                    "Sanity Test",
                    p, p => p.Test())).ToArray());

            // ---

            workflowCollection.AddWithOneIncrement(providers
                .OfType<ICanGenerateTemporarySecretValue>()
                .Select(DuplicateProvider)
                .Select(p => ProviderWorkflowAction.Create(
                    "Generate Temporary Secrets", 
                    p, p => p.GenerateTemporarySecretValue())).ToArray());

            workflowCollection.AddWithOneIncrement(providers
                .OfType<ICanDistributeTemporarySecretValues>()
                .Select(DuplicateProvider)
                .Select(p => ProviderWorkflowAction.Create(
                    "Distribute Temporary Secrets",
                    p, p =>
                {
                    var actions = workflowCollection.GetActions<ICanGenerateTemporarySecretValue, RegeneratedSecret>();
                    if (actions.Any())
                        return p.DistributeTemporarySecretValues(actions.Select(a => a.Result).ToList());
                    return Task.FromResult(false);
                })).ToArray());

            workflowCollection.AddWithOneIncrement(providers
                .OfType<ICanPerformUnifiedCommitForTemporarySecretValues>()
                .GroupBy(p => p.GenerateResourceIdentifierHashCode())
                .Select(p => p.First())
                .Select(DuplicateProvider)
                .Select(p => ProviderWorkflowAction.Create(
                    "Perform Unified Commit", 
                    p, p => p.UnifiedCommitForTemporarySecretValues())).ToArray());

            // ---

            workflowCollection.AddWithOneIncrement(providers
                .OfType<ICanRekey>()
                .Select(DuplicateProvider)
                .Select(p => ProviderWorkflowAction.Create(
                    "Rekey Object",
                    p, p => p.Rekey(validPeriod))).ToArray());

            workflowCollection.AddWithOneIncrement(providers
                .OfType<ICanDistributeLongTermSecretValues>()
                .Select(DuplicateProvider)
                .Select(p => ProviderWorkflowAction.Create(
                    "Distribute Rekeyed Secrets",
                    p, p =>
                {
                    return p.DistributeLongTermSecretValues(
                        workflowCollection.GetActions<ICanRekey, RegeneratedSecret>()
                                          .Select(a => a.Result).ToList());
                })).ToArray());

            workflowCollection.AddWithOneIncrement(providers
                .OfType<ICanPerformUnifiedCommit>()
                .GroupBy(p => p.GenerateResourceIdentifierHashCode())
                .Select(p => p.First())
                .Select(DuplicateProvider)
                .Select(p => ProviderWorkflowAction.Create(
                    "Perform Unified Commit on Rekeyed Secrets",
                    p, p => p.UnifiedCommit())).ToArray());

            // ---

            workflowCollection.AddWithOneIncrement(providers
                .OfType<ICanCleanup>()
                .Select(DuplicateProvider)
                .Select(p => ProviderWorkflowAction.Create(
                    "Cleanup",
                    p, p => p.Cleanup())).ToArray());

            return workflowCollection;
        }

        private ProviderWorkflowActionCollection CreateWorkflowCollection(
            TimeSpan secretValidPeriod,
            IEnumerable<ProviderExecutionParameters> providerConfigurations,
            AccessTokenCredential accessToken = null)
        {
            var providers = providerConfigurations.Select(r =>
            {
                var p = GetProviderInstance(
                    r.ProviderType,
                    r.ProviderConfiguration);
                if (r.AccessToken != null)
                    p.Credential = r.AccessToken;
                return p;
            }).ToList();

            var workflowCollection = CreateWorkflowCollection(
                secretValidPeriod,
                providers);

            if (accessToken != null)
                workflowCollection.EmbedCredentials(accessToken);

            return workflowCollection;
        }

        /// <summary>
        /// Execute the workflows for a given set of configured providers.
        /// 
        /// Providers will run either with the access token in the ProviderExecutionConfiguration
        /// object or with the AccessTokenCredential provided to this function.
        /// 
        /// Every (5) seconds, the periodicUpdateFunction will be invoked to update the
        /// caller on the status of the long-running action.
        /// </summary>
        /// <param name="providerConfigurations"></param>
        /// <param name="secretValidPeriod"></param>
        /// <param name="periodicUpdateFunction"></param>
        /// <param name="accessToken"></param>
        /// <returns></returns>
        public async Task ExecuteProviderWorkflows(
            IEnumerable<ProviderExecutionParameters> providerConfigurations,
            TimeSpan secretValidPeriod,
            Func<ProviderWorkflowActionCollection, Task> periodicUpdateFunction,
            AccessTokenCredential accessToken = null)
        {
            _logger.LogInformation("Creating workflow collection for {ProviderCount} provider instances", providerConfigurations.Count());
            var workflowCollection = CreateWorkflowCollection(
                secretValidPeriod,
                providerConfigurations,
                accessToken);

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

            logUpdateCancellationTokenSource.Cancel();
        }
    }
}