// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Automation.Cryptography.Default;
using AuthJanitor.Automation.SecureStorageProviders.AzureKeyVault;
using AuthJanitor.Automation.Shared;
using AuthJanitor.Automation.Shared.DataStores;
using AuthJanitor.Automation.Shared.Models;
using AuthJanitor.Providers;
using McMaster.NETCore.Plugins;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

[assembly: WebJobsStartup(typeof(AuthJanitor.Automation.Agent.Startup))]
namespace AuthJanitor.Automation.Agent
{
    public class Startup : IWebJobsStartup
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

        private static AuthJanitorServiceConfiguration ServiceConfiguration =>
            new AuthJanitorServiceConfiguration()
            {
                ApprovalRequiredLeadTimeHours = 24 * 7,
                AutomaticRekeyableJustInTimeLeadTimeHours = 24 * 2,
                AutomaticRekeyableTaskCreationLeadTimeHours = 24 * 7,
                ExternalSignalRekeyableLeadTimeHours = 24,
                MetadataStorageContainerName = "authjanitor",
                SecurePersistenceContainerName = "authjanitor",
                MasterEncryptionKey = "iamnotastrongkey!"
            };

        public void Configure(IWebJobsBuilder builder)
        {
            var logger = new LoggerFactory().CreateLogger(nameof(Startup));

            logger.LogDebug("Registering LoggerFactory");
            builder.Services.AddSingleton<ILoggerFactory>(new LoggerFactory());

            // TODO: Load ServiceConfguration from somewhere; right now these are just system defaults.
            logger.LogDebug("Registering Service Configuration");
            builder.Services.AddSingleton(ServiceConfiguration);

            logger.LogDebug("Registering Event Sinks");

            // TODO: Register IEventSinks here, before the EventDispatcherService
            //       This is where we offload to Azure Sentinel, send emails, etc.
            //       The *entire system* offloads to the EventDispatcherService to generalize events.

            logger.LogDebug("Registering Event Dispatcher");
            builder.Services.AddSingleton<EventDispatcherService>();

            logger.LogDebug("Registering Credential Provider Service");
            builder.Services.AddSingleton<CredentialProviderService>();

            logger.LogDebug("Registering Cryptographic Implementation");
            builder.Services.AddSingleton<ICryptographicImplementation>(
                new DefaultCryptographicImplementation(ServiceConfiguration.MasterEncryptionKey));

            logger.LogDebug("Registering Secure Storage Provider");
            builder.Services.AddSingleton<ISecureStorageProvider, KeyVaultSecureStorageProvider>();

            // -----

            logger.LogDebug("Registering DataStores");
            var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage", EnvironmentVariableTarget.Process);
            builder.Services.AddSingleton<IDataStore<ManagedSecret>>(
                new AzureBlobDataStore<ManagedSecret>(
                    connectionString,
                    ServiceConfiguration.MetadataStorageContainerName,
                    MANAGED_SECRETS_BLOB_NAME));
            builder.Services.AddSingleton<IDataStore<RekeyingTask>>(
                new AzureBlobDataStore<RekeyingTask>(
                    connectionString,
                    ServiceConfiguration.MetadataStorageContainerName,
                    REKEYING_TASKS_BLOB_NAME));
            builder.Services.AddSingleton<IDataStore<Resource>>(
                new AzureBlobDataStore<Resource>(
                    connectionString,
                    ServiceConfiguration.MetadataStorageContainerName,
                    RESOURCES_BLOB_NAME));
            builder.Services.AddSingleton<IDataStore<ScheduleWindow>>(
                new AzureBlobDataStore<ScheduleWindow>(
                    connectionString,
                    ServiceConfiguration.MetadataStorageContainerName,
                    SCHEDULES_BLOB_NAME));

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
