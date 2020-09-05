// Copyright (c) Microsoft Corporation.
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
        private const int MAX_RETRIES = 5;
        private const int DELAY_BETWEEN_ACTIONS_MS = 1000;
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

            // -----

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

            await PerformActionsInParallel(
                logger,
                providers.OfType<ICanPerformUnifiedCommitForTemporarySecretValues>()
                         .GroupBy(p => p.GenerateResourceIdentifierHashCode())
                         .Select(g => g.First()),
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

            await PerformActionsInParallel(
                logger,
                providers.OfType<ICanPerformUnifiedCommit>()
                         .GroupBy(p => p.GenerateResourceIdentifierHashCode())
                         .Select(g => g.First()),
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

            logger.IsComplete = true;
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
                    logger.LogInformation("Running action in serial on {0}", provider.GetType().Name);
                    await PerformAction(logger, provider, providerAction);
                    await Task.Delay(DELAY_BETWEEN_ACTIONS_MS / 2);
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
            var providerActions = providers.Select(async provider =>
            {
                await PerformActionsInSerial(
                    logger,
                    provider,
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

            if (providers.Any())
            {
                logger.LogInformation("Sleeping for {SleepTime}", DELAY_BETWEEN_ACTIONS_MS);
                await Task.Delay(DELAY_BETWEEN_ACTIONS_MS);
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
            var providerActions = providers.Select(async provider =>
            {
                try
                {
                    logger.LogInformation("Running action in parallel on {0}", provider.GetType().Name);
                    await PerformAction(logger, provider, providerAction);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, individualFailureErrorLogMessageTemplate, provider.GetType().Name);

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

            if (providers.Any())
            {
                logger.LogInformation("Sleeping for {SleepTime}", DELAY_BETWEEN_ACTIONS_MS);
                await Task.Delay(DELAY_BETWEEN_ACTIONS_MS);
            }
        }

        private static async Task PerformAction<TProviderType>(ILogger logger, TProviderType provider, Func<TProviderType, Task> providerAction)
        {
            for (var i = 0; i < MAX_RETRIES; i++)
            {
                try
                {
                    logger.LogInformation("Attempting action ({AttemptNumber}/{MaxAttempts})", i + 1, MAX_RETRIES);
                    await providerAction(provider);
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogWarning("Attempt failed! Exception was: {Exception}", ex);
                    // TODO: Providers need to be able to specify what Exceptions are ignored
                    //       For example, catching an invalid credential exception shouldn't do a retry
                    if (i == MAX_RETRIES - 1)
                        throw ex; // rethrow if at end of retries
                }
            }
        }
    }
}
