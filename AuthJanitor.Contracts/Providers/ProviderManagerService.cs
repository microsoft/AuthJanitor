// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
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
                    Details = type.GetCustomAttribute<ProviderAttribute>(),
                    SvgImage = type.GetCustomAttribute<ProviderImageAttribute>()?.SvgImage
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
            var rkoProviders = providers.OfType<IRekeyableObjectProvider>().ToList();
            var alcProviders = providers.OfType<IApplicationLifecycleProvider>().ToList();

            // NOTE: avoid costs of generating list of providers if information logging not turned on
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("RKO: {ProviderTypeNames}", string.Join(", ", rkoProviders.Select(p => p.GetType().Name)));
                logger.LogInformation("ALC: {ProviderTypeNames}", string.Join(", ", alcProviders.Select(p => p.GetType().Name)));
            }

            // -----

            logger.LogInformation("### Performing Provider Tests.");

            await PerformProviderActions(
                logger, 
                providers,
                p => p.Test(),
                "Error running sanity test on provider '{ProviderName}'",
                "Error running one or more sanity tests!");

            logger.LogInformation("### Retrieving/generating temporary secrets.");

            var temporarySecrets = new List<RegeneratedSecret>();
            await PerformProviderActions(
                logger,
                rkoProviders,
                p => p.GetSecretToUseDuringRekeying()
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

            logger.LogInformation("### Preparing {ProviderCount} Application Lifecycle Providers for rekeying...", alcProviders.Count);
            await PerformProviderActions(
                logger,
                alcProviders,
                p => p.BeforeRekeying(temporarySecrets),
                "Error preparing ALC provider '{ProviderName}'",
                "Error preparing one or more Application Lifecycle Providers for rekeying!");

            // -----

            logger.LogInformation("### Rekeying {ProviderCount} Rekeyable Object Providers...", rkoProviders.Count);
            var newSecrets = new List<RegeneratedSecret>();
            await PerformProviderActions(
                logger,
                rkoProviders,
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

            logger.LogInformation("### Committing {SecretCount} regenerated secrets to {ProviderCount} Application Lifecycle Providers...",
                newSecrets.Count,
                alcProviders.Count);

            await PerformProviderActions(
                logger,
                alcProviders,
                p => p.CommitNewSecrets(newSecrets),
                "Error committing to provider '{ProviderName}'",
                "Error committing regenerated secrets!");

            // -----

            logger.LogInformation("### Completing post-rekey operations on Application Lifecycle Providers...");

            await PerformProviderActions(
                logger,
                alcProviders,
                p => p.AfterRekeying(),
                "Error running post-rekey operations on provider '{ProviderName}'",
                "Error running post-rekey operations on one or more Application Lifecycle Providers!");
            
            // -----

            logger.LogInformation("### Completing finalizing operations on Rekeyable Object Providers...");

            await PerformProviderActions(
                logger,
                rkoProviders,
                p => p.OnConsumingApplicationSwapped(),
                "Error running after-swap operations on provider '{ProviderName}'",
                "Error running after-swap operations on one or more Rekeyable Object Providers!");

            logger.LogInformation("########## END REKEYING WORKFLOW ##########");
        }

        private static async Task PerformProviderActions<TProviderType>(
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
