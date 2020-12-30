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

        /// <summary>
        /// Check if a provider is loaded
        /// </summary>
        /// <param name="providerName">Provider type name</param>
        /// <returns><c>TRUE</c> if the provider is loaded</returns>
        public bool HasProvider(string providerName) => LoadedProviders.Any(p => p.ProviderTypeName == providerName);

        /// <summary>
        /// Get the metadata for a given Provider by its type name.
        /// 
        /// If the Provider is not found, an Exception is thrown.
        /// </summary>
        /// <param name="providerName">Provider type name</param>
        /// <returns>Provider's metadata</returns>
        public LoadedProviderMetadata GetProviderMetadata(string providerName)
        {
            if (!HasProvider(providerName))
                throw new Exception($"Provider '{providerName}' not available!");
            else
                return LoadedProviders.First(p => p.ProviderTypeName == providerName);
        }

        /// <summary>
        /// Get an unconfigured instance of a given Provider by its type name.
        /// 
        /// If the Provider is not found, an Exception is thrown.
        /// </summary>
        /// <param name="providerName">Provider type name</param>
        /// <returns>Unconfigured Provider instance</returns>
        public IAuthJanitorProvider GetProviderInstance(string providerName)
        {
            var metadata = GetProviderMetadata(providerName);
            return ActivatorUtilities.CreateInstance(_serviceProvider, metadata.ProviderType) as IAuthJanitorProvider;
        }

        /// <summary>
        /// Get an instance of a given Provider by its type name.
        /// The Provider will be configured with its defaults.
        /// 
        /// If the Provider is not found, an Exception is thrown.
        /// </summary>
        /// <param name="providerName">Provider type name</param>
        /// <returns>Provider instance with defaults</returns>
        public IAuthJanitorProvider GetProviderInstanceDefault(string providerName)
        {
            var instance = GetProviderInstance(providerName);
            instance.SerializedConfiguration = GetProviderConfiguration(providerName, GetProviderConfiguration(providerName));
            return instance;
        }

        /// <summary>
        /// Get an instance of a given Provider by its type name.
        /// The instance's configuration will be deserialized from 
        /// the given string.
        /// 
        /// If the Provider is not found, an Exception is thrown.
        /// </summary>
        /// <param name="providerName">Provider type name</param>
        /// <param name="serializedProviderConfiguration">Serialized configuration</param>
        /// <returns>Configured Provider instance</returns>
        public IAuthJanitorProvider GetProviderInstance(string providerName, string serializedProviderConfiguration)
        {
            var instance = GetProviderInstance(providerName);
            instance.SerializedConfiguration = serializedProviderConfiguration;
            return instance;
        }

        /// <summary>
        /// Create a duplicate of an existing Provider, including configuration.
        /// 
        /// If the Provider is not found, an Exception is thrown.
        /// </summary>
        /// <typeparam name="TProvider">Provider type</typeparam>
        /// <param name="existingProviderToClone">Provider to clone</param>
        /// <returns>Duplicate Provider instance</returns>
        public TProvider GetProviderInstance<TProvider>(TProvider existingProviderToClone)
            where TProvider : IAuthJanitorProvider =>
            (TProvider)GetProviderInstance(existingProviderToClone.GetType().AssemblyQualifiedName, existingProviderToClone.SerializedConfiguration);

        /// <summary>
        /// Get the default configuration for a given Provider by its type name
        /// </summary>
        /// <param name="name">Provider type name</param>
        /// <returns>Default configuration for Provider</returns>
        public AuthJanitorProviderConfiguration GetProviderConfiguration(string name) => ActivatorUtilities.CreateInstance(_serviceProvider, GetProviderMetadata(name).ProviderConfigurationType) as AuthJanitorProviderConfiguration;

        /// <summary>
        /// Get the Provider configuration for a given Provider from 
        /// a serialized string
        /// </summary>
        /// <param name="name">Provider type name</param>
        /// <param name="serializedConfiguration">Serialized configuration</param>
        /// <returns>Provider configuration</returns>
        public AuthJanitorProviderConfiguration GetProviderConfiguration(string name, string serializedConfiguration) => JsonSerializer.Deserialize(serializedConfiguration, GetProviderMetadata(name).ProviderConfigurationType, SerializerOptions) as AuthJanitorProviderConfiguration;
        
        /// <summary>
        /// Serialize a given Provider configuration
        /// </summary>
        /// <typeparam name="T">Configuration type</typeparam>
        /// <param name="name">Provider type name</param>
        /// <param name="configuration">Configuration object</param>
        /// <returns>Serialized configuration</returns>
        public string GetProviderConfiguration<T>(string name, T configuration) => JsonSerializer.Serialize(configuration, GetProviderMetadata(name).ProviderConfigurationType, SerializerOptions);

        /// <summary>
        /// Test a given serialized configuration with a given
        /// Provider type name
        /// </summary>
        /// <param name="name">Provider type name</param>
        /// <param name="serializedConfiguration">Serialized configuration to test</param>
        /// <returns><c>TRUE</c> if the configuration is valid for this Provider</returns>
        public bool TestProviderConfiguration(string name, string serializedConfiguration)
        {
            try { return GetProviderConfiguration(name, serializedConfiguration) != null; }
            catch { return false; }
        }

        /// <summary>
        /// Enumerate potential candidates for all loaded Provider modules using
        /// a given AccessTokenCredential
        /// </summary>
        /// <param name="credential">Credentials to access cloud service to enumerate</param>
        /// <returns>Collection of suggestions of Provider configurations based on existing services</returns>
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

        /// <summary>
        /// Enumerate potential candidates for a given Provider module using
        /// a given AccessTokenCredential
        /// </summary>
        /// <param name="credential">Credentials to access cloud service to enumerate</param>
        /// <param name="provider">Provider to enumerate</param>
        /// <returns>Collection of suggestions of Provider configurations based on existing services</returns>
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

        /// <summary>
        /// Loaded Providers available to this runtime instance
        /// </summary>
        public IReadOnlyList<LoadedProviderMetadata> LoadedProviders { get; }

        private TProvider DuplicateProvider<TProvider>(TProvider provider)
            where TProvider : IAuthJanitorProvider =>
            _serviceProvider.GetRequiredService<ProviderManagerService>()
                            .GetProviderInstance(provider);

        /// <summary>
        /// Create a Workflow Collection based on actions which need to
        /// be taken to execute a given set of Providers. This includes
        /// proper ordering of actions as required.
        /// </summary>
        /// <param name="validPeriod">Valid period for secret</param>
        /// <param name="providers">Providers to generate workflow collection from</param>
        /// <returns>Workflow collection</returns>
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
    }
}