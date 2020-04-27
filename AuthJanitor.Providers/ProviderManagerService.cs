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
            var testTasks = providers.Select(p => p.Test().ContinueWith(t =>
            {
                if (t.IsFaulted)
                    logger.LogError(t.Exception, "Error running sanity test on provider '{0}'", p.ProviderMetadata.Name);
            }));
            await Task.WhenAll(testTasks.ToArray());
            if (testTasks.Any(t => t.IsFaulted))
                throw new Exception("Error running one or more sanity tests!");

            // -----

            logger.LogInformation("### Getting temporary secrets from {0} Rekeyable Object Lifecycle Providers...", rkoProviders.Count());
            var tempSecretTasks = rkoProviders.Select(p => p.GetSecretToUseDuringRekeying().ContinueWith(t =>
            {
                if (t.IsFaulted)
                    logger.LogError(t.Exception, "Error getting temporary secret from provider '{0}'", p.ProviderMetadata.Name);
                return t.Result;
            }));
            await Task.WhenAll(tempSecretTasks.ToArray());
            if (tempSecretTasks.Any(t => t.IsFaulted))
                throw new Exception("Error retrieving temporary secrets from one or more Rekeyable Object Providers!");

            logger.LogInformation("{0} temporary secrets were created/read to be used during operation.", tempSecretTasks.Count(t => t.Result != null));

            // -----

            logger.LogInformation("### Preparing {0} Application Lifecycle Providers for rekeying...", alcProviders.Count());
            var prepareTasks = alcProviders.Select(p => p.BeforeRekeying(tempSecretTasks.Select(t => t.Result).ToList()).ContinueWith(t =>
            {
                if (t.IsFaulted)
                    logger.LogError(t.Exception, "Error preparing ALC provider '{0}'", p.GetType().Name);
            }));
            await Task.WhenAll(prepareTasks.ToArray());
            if (prepareTasks.Any(t => t.IsFaulted))
                throw new Exception("Error preparing one or more Application Lifecycle Providers for rekeying!");

            // -----

            logger.LogInformation("### Rekeying {0} Rekeyable Object Providers...", rkoProviders.Count());
            var rekeyingTasks = rkoProviders.Select(p => p.Rekey(validPeriod).ContinueWith(t =>
            {
                if (t.IsFaulted)
                    logger.LogError(t.Exception, "Error rekeying provider '{0}'", p.GetType().Name);
                return t.Result;
            }));
            await Task.WhenAll(rekeyingTasks.ToArray());
            if (rekeyingTasks.Any(t => t.IsFaulted))
                throw new Exception("Error rekeying one or more Rekeyable Object Providers!");

            logger.LogInformation("{0} secrets were regenerated.", rekeyingTasks.Count(t => t.Result != null));

            // -----

            logger.LogInformation("### Committing {0} regenerated secrets to {1} Application Lifecycle Providers...",
                rekeyingTasks.Count(t => t.Result != null),
                alcProviders.Count());
            var commitTasks = alcProviders.Select(p => p.CommitNewSecrets(rekeyingTasks.Select(t => t.Result).ToList()).ContinueWith(t =>
            {
                if (t.IsFaulted)
                    logger.LogError(t.Exception, "Error committing to provider '{0}'", p.GetType().Name);
            }));
            await Task.WhenAll(commitTasks.ToArray());
            if (commitTasks.Any(t => t.IsFaulted))
                throw new Exception("Error committing regenerated secrets!");

            // -----

            logger.LogInformation("### Completing post-rekey operations on Application Lifecycle Providers...");
            var postRekeyTasks = alcProviders.Select(p => p.AfterRekeying().ContinueWith(t =>
            {
                if (t.IsFaulted)
                    logger.LogError(t.Exception, "Error running post-rekey operations on provider '{0}'", p.GetType().Name);
            }));
            await Task.WhenAll(postRekeyTasks.ToArray());
            if (postRekeyTasks.Any(t => t.IsFaulted))
                throw new Exception("Error running post-rekey operations on one or more Application Lifecycle Providers!");

            // -----

            logger.LogInformation("### Completing finalizing operations on Rekeyable Object Providers...");
            var afterSwapTasks = rkoProviders.Select(p => p.OnConsumingApplicationSwapped().ContinueWith(t =>
            {
                if (t.IsFaulted)
                    logger.LogError(t.Exception, "Error running after-swap operations on provider '{0}'", p.GetType().Name);
            }));
            await Task.WhenAll(afterSwapTasks.ToArray());
            if (afterSwapTasks.Any(t => t.IsFaulted))
                throw new Exception("Error running after-swap operations on one or more Rekeyable Object Providers!");


            logger.LogInformation("########## END REKEYING WORKFLOW ##########");
        }
    }
}
