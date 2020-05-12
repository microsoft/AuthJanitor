// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Automation.Shared;
using AuthJanitor.Automation.Shared.Models;
using AuthJanitor.Integrations.CryptographicImplementations;
using AuthJanitor.Integrations.CryptographicImplementations.Default;
using AuthJanitor.Integrations.DataStores;
using AuthJanitor.Integrations.DataStores.AzureBlobStorage;
using AuthJanitor.Integrations.IdentityServices;
using AuthJanitor.Integrations.IdentityServices.AzureActiveDirectory;
using AuthJanitor.Integrations.SecureStorage;
using AuthJanitor.Integrations.SecureStorage.AzureKeyVault;
using AuthJanitor.Providers;
using McMaster.NETCore.Plugins;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;

[assembly: FunctionsStartup(typeof(AuthJanitor.Automation.Agent.Startup))]
namespace AuthJanitor.Automation.Agent
{
    public class Startup : FunctionsStartup
    {
        private const string RESOURCES_BLOB_NAME = "resources";
        private const string MANAGED_SECRETS_BLOB_NAME = "secrets";
        private const string REKEYING_TASKS_BLOB_NAME = "tasks";
        private const string SCHEDULES_BLOB_NAME = "schedules";

        private const string PROVIDER_SEARCH_MASK = "AuthJanitor.Providers.*.dll";
        private static readonly string PROVIDER_SEARCH_PATH = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), ".."));
        private static readonly Type[] PROVIDER_SHARED_TYPES = new Type[]
        {
            typeof(IAuthJanitorProvider),
            typeof(AuthJanitorProvider<>),
            typeof(IApplicationLifecycleProvider),
            typeof(ApplicationLifecycleProvider<>),
            typeof(IRekeyableObjectProvider),
            typeof(RekeyableObjectProvider<>),
            typeof(IServiceCollection),
            typeof(ILogger)
        };

        public static IServiceProvider ServiceProvider { get; set; }

        private static IFunctionsHostBuilder AddConfiguration(IFunctionsHostBuilder builder, Func<IConfigurationBuilder, IConfiguration> configurationBuilderFunc)
        {
            IConfigurationBuilder configurationBuilder = new ConfigurationBuilder();
            var configurationService = builder.Services.FirstOrDefault(d => d.ServiceType == typeof(IConfiguration));
            if (configurationService?.ImplementationInstance is IConfiguration configRoot)
                configurationBuilder.AddConfiguration(configRoot);
            configurationBuilder = configurationBuilder.SetBasePath(PROVIDER_SEARCH_PATH);

            builder.Services.Replace(
                ServiceDescriptor.Singleton(typeof(IConfiguration), configurationBuilderFunc(configurationBuilder)));
            return builder;
        }

        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddOptions();

            var logger = new LoggerFactory().CreateLogger(nameof(Startup));

            logger.LogDebug("Registering LoggerFactory");
            builder.Services.AddSingleton<ILoggerFactory>(new LoggerFactory());

            logger.LogDebug("Registering Azure AD Identity Service");
            builder.Services.AddAJAzureActiveDirectory<AzureADIdentityServiceConfiguration>(o =>
            {
                o.ClientId = "clientId";
                o.ClientSecret = "clientSecret";
                o.TenantId = "tenantId";
            });

            logger.LogDebug("Registering Event Sinks");

            // TODO: Register IEventSinks here, before the EventDispatcherService
            //       This is where we offload to Azure Sentinel, send emails, etc.
            //       The *entire system* offloads to the EventDispatcherService to generalize events.

            logger.LogDebug("Registering Cryptographic Implementation");
            builder.Services.AddAJDefaultCryptographicImplementation<DefaultCryptographicImplementationConfiguration>(o => 
            {
                o.MasterEncryptionKey = "weakkey";
            });

            logger.LogDebug("Registering Secure Storage Provider");
            builder.Services.AddAJAzureKeyVault<KeyVaultSecureStorageProviderConfiguration>(o =>
            {
                o.VaultName = "vault";
            });

            logger.LogDebug("Registering AuthJanitor MetaServices");
            AuthJanitorServiceRegistration.RegisterServices(builder.Services);

            // -----

            logger.LogDebug("Registering DataStores");
            builder.Services.AddAJAzureBlobStorage<AzureBlobStorageDataStoreConfiguration>(o =>
            {
                o.ConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage", EnvironmentVariableTarget.Process);
                o.Container = "authjanitor";
            });

            // -----

            logger.LogDebug("Registering ViewModel generators");
            ViewModelFactory.ConfigureServices(builder.Services);

            // -----

            logger.LogDebug("Scanning for Provider modules at {0}\\{1} recursively", PROVIDER_SEARCH_PATH, PROVIDER_SEARCH_MASK);

            var providerTypes = Directory.GetFiles(PROVIDER_SEARCH_PATH, PROVIDER_SEARCH_MASK, new EnumerationOptions() { RecurseSubdirectories = true })
                                         .SelectMany(libraryFile => PluginLoader.CreateFromAssemblyFile(libraryFile, PROVIDER_SHARED_TYPES)
                                                                            .LoadDefaultAssembly()
                                                                            .GetTypes()
                                                                            .Where(type => !type.IsAbstract && typeof(IAuthJanitorProvider).IsAssignableFrom(type)))
                                         .ToArray();

            logger.LogInformation("Found {0} providers: {1}", providerTypes.Count(), string.Join("  ", providerTypes.Select(t => t.Name)));
            logger.LogInformation("Registering Provider Manager Service");
            ProviderManagerService.ConfigureServices(builder.Services, providerTypes);

            // -----

            ServiceProvider = builder.Services.BuildServiceProvider();
        }
    }
}
