// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Integrations.CryptographicImplementations;
using AuthJanitor.Providers.Azure;
using AuthJanitor.Providers.Capabilities;
using Microsoft.Azure.Management.AppService.Fluent;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core.CollectionActions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.AppServices.Functions
{
    [Provider(Name = "Functions App Key",
              Description = "Regenerates a Function Key for an Azure Functions application",
              SvgImage = ProviderImages.FUNCTIONS_SVG)]
    public class FunctionKeyRekeyableObjectProvider : 
        AzureAuthJanitorProvider<FunctionKeyConfiguration, IFunctionApp>,
        ICanRekey,
        ICanRunSanityTests,
        ICanEnumerateResourceCandidates
    {
        private readonly ICryptographicImplementation _cryptographicImplementation;
        private readonly ILogger _logger;

        public FunctionKeyRekeyableObjectProvider(
            ILogger<FunctionKeyRekeyableObjectProvider> logger,
            ICryptographicImplementation cryptographicImplementation)
        {
            _logger = logger;
            _cryptographicImplementation = cryptographicImplementation;
        }

        public async Task Test()
        {
            var functionsApp = await GetResourceAsync();
            if (functionsApp == null)
                throw new Exception($"Cannot locate Functions application called '{Configuration.ResourceName}' in group '{Configuration.ResourceGroup}'");
            var keys = await functionsApp.ListFunctionKeysAsync(Configuration.FunctionName);
            if (keys == null)
                throw new Exception($"Cannot list Function Keys for Function '{Configuration.FunctionName}'");
        }

        public async Task<RegeneratedSecret> Rekey(TimeSpan requestedValidPeriod)
        {
            _logger.LogInformation("Generating a new secret of length {SecretKeyLength}", Configuration.KeyLength);
            RegeneratedSecret newKey = new RegeneratedSecret()
            {
                Expiry = DateTimeOffset.UtcNow + requestedValidPeriod,
                UserHint = Configuration.UserHint,
                NewSecretValue = await _cryptographicImplementation.GenerateCryptographicallyRandomSecureString(Configuration.KeyLength)
            };

            var functionsApp = await GetResourceAsync();
            if (functionsApp == null)
                throw new Exception($"Cannot locate Functions application called '{Configuration.ResourceName}' in group '{Configuration.ResourceGroup}'");

            _logger.LogInformation("Removing previous Function Key '{FunctionKeyName}' from Function '{FunctionName}'", Configuration.FunctionKeyName, Configuration.FunctionName);
            await functionsApp.RemoveFunctionKeyAsync(Configuration.FunctionName, Configuration.FunctionKeyName);

            _logger.LogInformation("Adding new Function Key '{FunctionKeyName}' from Function '{FunctionName}'", Configuration.FunctionKeyName, Configuration.FunctionName);
            await functionsApp.AddFunctionKeyAsync(Configuration.FunctionName, Configuration.FunctionKeyName, newKey.NewSecretValue.GetNormalString());

            return newKey;
        }

        public override string GetDescription() =>
            $"Regenerates a Functions key for an Azure " +
            $"Functions application called {Configuration.ResourceName} (Resource Group " +
            $"'{Configuration.ResourceGroup}').";

        protected override ISupportsGettingByResourceGroup<IFunctionApp> GetResourceCollection(IAzure azure) => azure.AppServices.FunctionApps;

        public async Task<List<ProviderResourceSuggestion>> EnumerateResourceCandidates(AuthJanitorProviderConfiguration baseConfig)
        {
            var azureConfig = baseConfig as AzureAuthJanitorProviderConfiguration;
            
            IPagedCollection<IFunctionApp> items;
            if (!string.IsNullOrEmpty(azureConfig.ResourceGroup))
                items = await (await GetAzureAsync()).AppServices.FunctionApps.ListByResourceGroupAsync(azureConfig.ResourceGroup);
            else
                items = await (await GetAzureAsync()).AppServices.FunctionApps.ListAsync();

            return (await Task.WhenAll(items.Select(async i =>
            {
                var items = new List<ProviderResourceSuggestion>();
                try
                {
                    foreach (var func in await i.ListFunctionsAsync())
                    {
                        foreach (var key in await i.ListFunctionKeysAsync(func.Name))
                        {
                            items.Add(
                                new ProviderResourceSuggestion()
                                {
                                    Configuration = new FunctionKeyConfiguration()
                                    {
                                        ResourceName = i.Name,
                                        ResourceGroup = i.ResourceGroupName,
                                        FunctionName = func.Name,
                                        FunctionKeyName = key.Key,
                                        KeyLength = key.Value.Length > 10 ? key.Value.Length : 32
                                    },
                                    Name = $"Function Key - {i.ResourceGroupName} - {i.Name} - {func.Name} ({key.Key})",
                                    ProviderType = this.GetType().AssemblyQualifiedName,
                                    ResourceValues = new[] { key.Value },
                                    AddressableNames = i.EnabledHostNames.ToList()
                                }) ;
                        }
                    }
                }
                catch (Exception) { }
                return items;
            }))).SelectMany(f => f).ToList();
        }

        // TODO: Zero-downtime rotation here with similar slotting?
        //During the rekeying, the Functions App will " +
        //    $"be moved from slot '{Configuration.SourceSlot}' to slot '{Configuration.TemporarySlot}' " +
        //    $"temporarily, and then to slot '{Configuration.DestinationSlot}'.";
    }
}
