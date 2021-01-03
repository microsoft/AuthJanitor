using AuthJanitor.CryptographicImplementations;
using AuthJanitor.IdentityServices;
using AuthJanitor.Providers;
using AuthJanitor.SecureStorage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace AuthJanitor
{
    public static class AuthJanitorServiceExtensions
    {
        private const string PROVIDER_SEARCH_MASK = "AuthJanitor.Providers.*.dll";
        private static readonly string PROVIDER_SEARCH_PATH = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), ".."));

        public static void AddAuthJanitorService(this IServiceCollection serviceCollection,
            Func<string, Assembly> assemblyLoaderCallback,
            string instanceIdentity)
        {
            serviceCollection.AddSingleton((s) => new ProviderManagerService(s,
                LoadAssembliesFromLocalPath(assemblyLoaderCallback)));

            serviceCollection.Configure<AuthJanitorServiceOptions>((o) => o.InstanceId = instanceIdentity);

            serviceCollection.RegisterAuthJanitorRollupServices();
        }

        public static void AddAuthJanitorDummyServices
            (this IServiceCollection serviceCollection)
        {
            serviceCollection.AddAJDefaultCryptographicImplementation
                <DefaultCryptographicImplementationConfiguration>(o =>
            {
                o.EmbedEphemeralRSAKey();
            });
            serviceCollection.AddDummyAgentCommunicationProvider();
            serviceCollection.AddDummyIdentityService();
            serviceCollection.AddDummySecureStorage();
        }

        private static void RegisterAuthJanitorRollupServices(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<EventDispatcherService>();
            //serviceCollection.AddSingleton<SystemIntegrityService>();

            serviceCollection.AddTransient<ProviderWorkflowActionLogger>();
            serviceCollection.AddTransient(typeof(ProviderWorkflowActionLogger<>), typeof(ProviderWorkflowActionLogger<>));
            
            serviceCollection.AddSingleton<AuthJanitorService>();

        }

        private static Type[] LoadAssembliesFromLocalPath(Func<string, Assembly> assemblyLoaderCallback) =>
            Directory.GetFiles(
                PROVIDER_SEARCH_PATH,
                PROVIDER_SEARCH_MASK,
                new EnumerationOptions() { RecurseSubdirectories = true })
                .SelectMany(libraryFile => assemblyLoaderCallback(libraryFile)
                    .GetTypes()
                    .Where(type => !type.IsAbstract && typeof(IAuthJanitorProvider).IsAssignableFrom(type)))
                .ToArray();
    }
}
