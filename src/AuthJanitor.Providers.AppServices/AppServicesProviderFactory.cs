// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.IdentityServices;
using AuthJanitor.Integrations.CryptographicImplementations;
using AuthJanitor.Providers.AppServices.Functions;
using AuthJanitor.Providers.Azure;
using AuthJanitor.Providers.Capabilities;
using Microsoft.Azure.Management.AppService.Fluent;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.AppServices
{
    public class AppServicesProviderFactory : IProviderFactory
    {
        private readonly ILogger<AppServicesProviderFactory> _logger;
        private readonly ProviderManagerService _providerManagerService;
        private readonly IIdentityService _identityService;
        private readonly ICryptographicImplementation _cryptography;

        public AppServicesProviderFactory(
            ILogger<AppServicesProviderFactory> logger,
            IIdentityService identityService,
            ICryptographicImplementation cryptography,
            ProviderManagerService providerManagerService)
        {
            _logger = logger;
            _identityService = identityService;
            _cryptography = cryptography;
            _providerManagerService = providerManagerService;
        }

        public Task<List<ProviderResourceCandidate>> GetProviders() => Task.FromResult(new List<ProviderResourceCandidate>());
        public async Task GetProviders(List<ProviderResourceCandidate> providers) 
        {
            var candidates = new List<ProviderResourceCandidate>();
            var token = await _identityService.GetAccessTokenOnBehalfOfCurrentUserAsync();
            var azure = await Microsoft.Azure.Management.Fluent.Azure
                    .Configure()
                    .Authenticate(token.CreateAzureCredentials())
                    .WithDefaultSubscriptionAsync();

            var functionApps = await azure.AppServices.FunctionApps.ListAsync();
            foreach (var functionApp in functionApps)
            {
                var appSettings = await functionApp.GetAppSettingsAsync();
                var connStrings = await functionApp.GetConnectionStringsAsync();

                // Todo: remove .Result
                var appSettingsDict = appSettings.ToDictionary(
                    k => k.Key,
                    v => _cryptography.Hash(v.Value.Value).Result);
                var connStringsDict = connStrings.ToDictionary(
                    k => k.Key,
                    v => _cryptography.Hash(v.Value.Value).Result);

                foreach (var provider in providers)
                {
                    foreach (var appSetting in appSettingsDict.Where(i => provider.HasSecretHash(i.Value)))
                        provider.ApplicationLifecycleProviders.Add(appSettingsDict[appSetting.Key], CreateFromApp(functionApp, appSettings[appSetting.Key]));
                    foreach (var connString in connStringsDict.Where(i => provider.HasSecretHash(i.Value)))
                        provider.ApplicationLifecycleProviders.Add(connStringsDict[connString.Key], CreateFromApp(functionApp, connStrings[connString.Key]));
                }
            }
        }

        private AppSettingsFunctionsApplicationLifecycleProvider CreateFromApp(IFunctionApp functionApp, IAppSetting appSetting)
        {
            var providerTemplate = _providerManagerService.GetProviderInstance(typeof(Functions.AppSettingsFunctionsApplicationLifecycleProvider).AssemblyQualifiedName)
                as AppSettingsFunctionsApplicationLifecycleProvider;
            providerTemplate.Configuration.ResourceName = functionApp.Name;
            providerTemplate.Configuration.ResourceGroup = functionApp.ResourceGroupName;
            providerTemplate.Configuration.SettingName = appSetting.Key;
            // providerTemplate.Configuration.CommitAsConnectionString = ???
            // todo: detect conn string
            return providerTemplate;
        }
        private ConnectionStringFunctionsApplicationLifecycleProvider CreateFromApp(IFunctionApp functionApp, IConnectionString connString)
        {
            var providerTemplate = _providerManagerService.GetProviderInstance(typeof(Functions.ConnectionStringFunctionsApplicationLifecycleProvider).AssemblyQualifiedName)
                as ConnectionStringFunctionsApplicationLifecycleProvider;
            providerTemplate.Configuration.ResourceName = functionApp.Name;
            providerTemplate.Configuration.ResourceGroup = functionApp.ResourceGroupName;
            providerTemplate.Configuration.ConnectionStringName = connString.Name;
            providerTemplate.Configuration.ConnectionStringType = connString.Type;
            return providerTemplate;
        }
    }
}
