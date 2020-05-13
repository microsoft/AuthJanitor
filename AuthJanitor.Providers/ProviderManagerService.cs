// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace AuthJanitor.Providers
{

    public class ProviderManagerService
    {
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
        public AuthJanitorProviderConfiguration GetProviderConfiguration(string name, string serializedConfiguration) => JsonConvert.DeserializeObject(serializedConfiguration, GetProviderMetadata(name).ProviderConfigurationType) as AuthJanitorProviderConfiguration;
        public IReadOnlyList<LoadedProviderMetadata> LoadedProviders { get; }

        public async Task ExecuteRekeyingWorkflow(
            RekeyingAttemptLogger logger,
            TimeSpan validPeriod,
            IEnumerable<IAuthJanitorProvider> providers)
        {
            logger.LogInformation("########## BEGIN REKEYING WORKFLOW ##########");
            var rkoProviders = providers.Where(p => p is IRekeyableObjectProvider).Cast<IRekeyableObjectProvider>();
            var alcProviders = providers.Where(p => p is IApplicationLifecycleProvider).Cast<IApplicationLifecycleProvider>();
            logger.LogInformation("RKO: {0}", string.Join(", ", rkoProviders.Select(p => p.GetType().Name)));
            logger.LogInformation("ALC: {0}", string.Join(", ", alcProviders.Select(p => p.GetType().Name)));

            // -----

            logger.LogInformation("### Performing Provider Tests.");

            await PerformProviderActions(
                logger, 
                providers,
                p => p.Test(),
                "Error running sanity test on provider '{0}'",
                "Error running one or more sanity tests!");

            logger.LogInformation("### Retrieving/generating temporary secrets.");

            var temporarySecrets = new List<RegeneratedSecret>();
            await PerformProviderActions(
                logger,
                rkoProviders,
                p => p.GetSecretToUseDuringRekeying().ContinueWith(s => temporarySecrets.Add(s.Result)),
                "Error getting temporary secret from provider '{0}'",
                "Error retrieving temporary secrets from one or more Rekeyable Object Providers!");

            temporarySecrets.RemoveAll(s => s == null);
            logger.LogInformation("{0} temporary secrets were created/read to be used during operation.", temporarySecrets.Count);

            // ---

            logger.LogInformation("### Preparing {0} Application Lifecycle Providers for rekeying...", alcProviders.Count());
            await PerformProviderActions(
                logger,
                alcProviders,
                p => p.BeforeRekeying(temporarySecrets),
                "Error preparing ALC provider '{0}'",
                "Error preparing one or more Application Lifecycle Providers for rekeying!");

            // -----

            logger.LogInformation("### Rekeying {0} Rekeyable Object Providers...", rkoProviders.Count());
            var newSecrets = new List<RegeneratedSecret>();
            await PerformProviderActions(
                logger,
                rkoProviders,
                p => p.Rekey(validPeriod),
                "Error rekeying provider '{0}'",
                "Error rekeying one or more Rekeyable Object Providers!");

            newSecrets.RemoveAll(s => s == null);
            logger.LogInformation("{0} secrets were regenerated.", newSecrets.Count);

            // -----

            logger.LogInformation("### Committing {0} regenerated secrets to {1} Application Lifecycle Providers...",
                newSecrets.Count,
                alcProviders.Count());

            await PerformProviderActions(
                logger,
                alcProviders,
                p => p.CommitNewSecrets(newSecrets),
                "Error committing to provider '{0}'",
                "Error committing regenerated secrets!");

            // -----

            logger.LogInformation("### Completing post-rekey operations on Application Lifecycle Providers...");

            await PerformProviderActions(
                logger,
                alcProviders,
                p => p.AfterRekeying(),
                "Error running post-rekey operations on provider '{0}'",
                "Error running post-rekey operations on one or more Application Lifecycle Providers!");
            
            // -----

            logger.LogInformation("### Completing finalizing operations on Rekeyable Object Providers...");

            await PerformProviderActions(
                logger,
                rkoProviders,
                p => p.OnConsumingApplicationSwapped(),
                "Error running after-swap operations on provider '{0}'",
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
            var providerActions = providers.Select(p => providerAction(p)
                .ContinueWith(t =>
                {
                    logger.LogError(t.Exception, individualFailureErrorLogMessageTemplate, p.GetType().Name);
                },
                TaskContinuationOptions.OnlyOnFaulted));

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
