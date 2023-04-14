// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.CLI.Verbs;
using AuthJanitor.IdentityServices;
using AuthJanitor.Providers;
using AuthJanitor.SecureStorage;
using CommandLine;
using McMaster.NETCore.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace AuthJanitor.CLI
{
    class Program
    {
        private const string PROVIDER_SEARCH_MASK = "AuthJanitor.Providers.*.dll";
        private static readonly string PROVIDER_SEARCH_PATH = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), ".."));
        private static readonly Type[] PROVIDER_SHARED_TYPES = new Type[]
        {
            typeof(IAuthJanitorProvider),
            typeof(AuthJanitorProvider<>),
            typeof(IServiceCollection),
            typeof(ILogger)
        };

        private static ServiceCollection BuildServiceCollection()
        {
            var serviceCollection = new ServiceCollection();
            var logger = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug)
                       .AddConsole();
            }).CreateLogger<Program>();

            serviceCollection.AddOptions();

            serviceCollection.AddAuthJanitorDummyServices();

            logger.LogDebug("Scanning for Provider modules at {ProviderSearchPath}\\{ProviderSearchMask} recursively", PROVIDER_SEARCH_PATH, PROVIDER_SEARCH_MASK);

            var providerTypes = Directory.GetFiles(PROVIDER_SEARCH_PATH, PROVIDER_SEARCH_MASK, new EnumerationOptions() { RecurseSubdirectories = true })
                                         .SelectMany(libraryFile => PluginLoader.CreateFromAssemblyFile(libraryFile, PROVIDER_SHARED_TYPES)
                                                                            .LoadDefaultAssembly()
                                                                            .GetTypes()
                                                                            .Where(type => !type.IsAbstract && typeof(IAuthJanitorProvider).IsAssignableFrom(type)))
                                         .ToArray();

            logger.LogInformation("Found {ProviderCount} providers: {ProviderTypeNames}", providerTypes.Length, string.Join("  ", providerTypes.Select(t => t.Name)));

            logger.LogDebug("Registering AuthJanitor Services");
            serviceCollection.AddAuthJanitorService(
                (a) => PluginLoader.CreateFromAssemblyFile(a, AuthJanitorService.ProviderSharedTypes)
                                   .LoadDefaultAssembly(),
                "cli");

            return serviceCollection;
        }

        static void Main(string[] args)
        {
            var serviceProvider = BuildServiceCollection()
                .AddLogging()
                .BuildServiceProvider();

            var logger = serviceProvider.GetService<ILoggerFactory>().CreateLogger<Program>();
            logger.LogDebug("Starting application");

            var ajService = serviceProvider.GetRequiredService<AuthJanitorService>();

            Parser.Default.ParseArguments<
                EnumerateProviderConfigurationsVerb,
                RunProviderSetVerb>(args)
                .WithParsed<EnumerateProviderConfigurationsVerb>(async v => await v.Run(logger, ajService))
                .WithParsed<RunProviderSetVerb>(async v => await v.Run(logger, ajService));
        }
    }
}
