﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
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
using System.Threading.Tasks;

namespace AuthJanitor.Providers
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

        private readonly IServiceProvider _serviceProvider;

        public ProviderManagerService(
            IServiceProvider serviceProvider,
            params Type[] providerTypes)
        {
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

        public static void ConfigureServices(IServiceCollection serviceCollection, params Type[] loadedProviderTypes)
        {
            serviceCollection.AddSingleton<ProviderManagerService>((s) => new ProviderManagerService(s, loadedProviderTypes));
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

        public IAuthJanitorProvider GetProviderInstance(string providerName, string serializedProviderConfiguration)
        {
            var instance = GetProviderInstance(providerName);
            instance.SerializedConfiguration = serializedProviderConfiguration;
            return instance;
        }

        public AuthJanitorProviderConfiguration GetProviderConfiguration(string name) => ActivatorUtilities.CreateInstance(_serviceProvider, GetProviderMetadata(name).ProviderConfigurationType) as AuthJanitorProviderConfiguration;
        public AuthJanitorProviderConfiguration GetProviderConfiguration(string name, string serializedConfiguration) => JsonSerializer.Deserialize(serializedConfiguration, GetProviderMetadata(name).ProviderConfigurationType, SerializerOptions) as AuthJanitorProviderConfiguration;

        public bool TestProviderConfiguration(string name, string serializedConfiguration)
        {
            try
            {
                var metadata = GetProviderMetadata(name);
                var obj = JsonSerializer.Deserialize(serializedConfiguration, metadata.ProviderConfigurationType, SerializerOptions);
                return obj != null;
            }
            catch { return false; }
        }

        public IReadOnlyList<LoadedProviderMetadata> LoadedProviders { get; }

        public async Task ExecuteRekeyingWorkflow(
            RekeyingAttemptLogger logger,
            TimeSpan validPeriod,
            IEnumerable<IAuthJanitorProvider> providers)
        {
            logger.LogInformation("########## BEGIN REKEYING WORKFLOW ##########");

            // -----

            logger.LogInformation("### Performing provider tests...");

            await PerformActionsInParallel(
                logger,
                providers.OfType<ICanRunSanityTests>(),
                p => p.Test(),
                "Error running sanity test on provider '{ProviderName}'",
                "Error running one or more sanity tests!");

            // -----

            logger.LogInformation("### Retrieving/generating temporary secrets...");

            var temporarySecrets = new List<RegeneratedSecret>();
            await PerformActionsInParallel(
                logger,
                providers.OfType<ICanGenerateTemporarySecretValue>(),
                p => p.GenerateTemporarySecretValue()
                      .ContinueWith(t =>
                      {
                          if (t.Result != null)
                          {
                              temporarySecrets.Add(t.Result);
                          }
                      }),
                "Error getting temporary secret from provider '{ProviderName}'",
                "Error retrieving temporary secrets from one or more Rekeyable Object Providers!");

            logger.LogInformation("{SecretCount} temporary secrets were created/read to be used during operation.", temporarySecrets.Count);

            // ---

            logger.LogInformation("### Distributing temporary secrets...");

            await PerformActionsInParallelGroups(
                logger,
                providers.OfType<ICanDistributeTemporarySecretValues>()
                         .GroupBy(p => p.GenerateResourceIdentifierHashCode()),
                p => p.DistributeTemporarySecretValues(temporarySecrets),
                "Error distributing secrets to ALC provider '{ProviderName}'",
                "Error distributing secrets!");

            // -----

            logger.LogInformation("### Performing commits for temporary secrets...");

            await PerformActionsInParallelGroups(
                logger,
                providers.OfType<ICanPerformUnifiedCommitForTemporarySecretValues>()
                         .GroupBy(p => p.GenerateResourceIdentifierHashCode()),
                p => p.UnifiedCommitForTemporarySecretValues(),
                "Error committing temporary secrets for ALC provider '{ProviderName}'",
                "Error committing temporary secrets!");

            // -----

            logger.LogInformation("### Rekeying objects and services...");

            var newSecrets = new List<RegeneratedSecret>();
            await PerformActionsInParallel(
                logger,
                providers.OfType<IRekeyableObjectProvider>(),
                p => p.Rekey(validPeriod)
                        .ContinueWith(t => 
                        { 
                            if (t.Result != null) 
                            {
                                newSecrets.Add(t.Result);
                            } 
                        }),
                "Error rekeying provider '{ProviderName}'",
                "Error rekeying one or more Rekeyable Object Providers!");

            logger.LogInformation("{SecretCount} secrets were regenerated.", newSecrets.Count);

            // -----

            logger.LogInformation("### Distributing regenerated secrets...");

            await PerformActionsInParallelGroups(
                logger,
                providers.OfType<IApplicationLifecycleProvider>()
                         .GroupBy(p => p.GenerateResourceIdentifierHashCode()),
                p => p.DistributeLongTermSecretValues(newSecrets),
                "Error committing to provider '{ProviderName}'",
                "Error committing regenerated secrets!");

            // -----

            logger.LogInformation("### Performing commits...");

            await PerformActionsInParallelGroups(
                logger,
                providers.OfType<ICanPerformUnifiedCommit>()
                         .GroupBy(p => p.GenerateResourceIdentifierHashCode()),
                p => p.UnifiedCommit(),
                "Error committing secrets for ALC provider '{ProviderName}'",
                "Error committing secrets!");

            // -----

            logger.LogInformation("### Running cleanup operations...");

            await PerformActionsInParallelGroups(
                logger,
                providers.OfType<ICanCleanup>()
                         .GroupBy(p => p.GenerateResourceIdentifierHashCode()),
                p => p.Cleanup(),
                "Error cleaning up provider '{ProviderName}'",
                "Error cleaning up!");

            logger.LogInformation("########## END REKEYING WORKFLOW ##########");
        }

        private static async Task PerformActionsInSerial<TProviderType>(
            ILogger logger, 
            IEnumerable<TProviderType> providers, 
            Func<TProviderType, Task> providerAction, 
            string individualFailureErrorLogMessageTemplate)
            where TProviderType : IAuthJanitorProvider
        {
            foreach (var provider in providers)
            {
                try
                {
                    await providerAction(provider);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, individualFailureErrorLogMessageTemplate, provider.GetType().Name);

                    throw;
                }
            }
        }

        private static async Task PerformActionsInParallelGroups<TProviderType>(
            ILogger logger, 
            IEnumerable<IGrouping<int, TProviderType>> providers, 
            Func<TProviderType, Task> providerAction, 
            string individualFailureErrorLogMessageTemplate, 
            string anyFailureExceptionMessage)
            where TProviderType : IAuthJanitorProvider
        {
            var providerActions = providers.Select(async p =>
            {
                await PerformActionsInSerial(
                    logger,
                    p,
                    providerAction,
                    individualFailureErrorLogMessageTemplate);
            });

            try
            {
                await Task.WhenAll(providerActions);
            }
            catch (Exception exception)
            {
                throw new Exception(anyFailureExceptionMessage, exception);
            }
        }

        private static async Task PerformActionsInParallel<TProviderType>(
            ILogger logger, 
            IEnumerable<TProviderType> providers, 
            Func<TProviderType, Task> providerAction, 
            string individualFailureErrorLogMessageTemplate, 
            string anyFailureExceptionMessage)
            where TProviderType : IAuthJanitorProvider
        {
            var providerActions = providers.Select(async p =>
            {
                try
                {
                    await providerAction(p);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, individualFailureErrorLogMessageTemplate, p.GetType().Name);

                    throw;
                }
            });

            try
            {
                await Task.WhenAll(providerActions);
            }
            catch (Exception exception)
            {
                throw new Exception(anyFailureExceptionMessage, exception);
            }
        }
    }
}
