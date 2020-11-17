// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Integrations.CryptographicImplementations;
using AuthJanitor.Providers.Azure;
using AuthJanitor.Providers.Azure.Workflows;
using AuthJanitor.Providers.Capabilities;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core.CollectionActions;
using Microsoft.Azure.Management.Search.Fluent;
using Microsoft.Azure.Management.Search.Fluent.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.AzureSearch
{
    [Provider(Name = "Azure Search Admin Key",
              Description = "Regenerates an Admin Key for an Azure Search service",
              SvgImage = ProviderImages.AZURE_SEARCH_SVG)]
    public class AzureSearchAdminKeyRekeyableObjectProvider : TwoKeyAzureRekeyableObjectProvider<AzureSearchAdminKeyConfiguration, ISearchService, IAdminKeys, AzureSearchAdminKeyConfiguration.AzureSearchKeyKinds, AdminKeyKind>,
        ICanEnumerateResourceCandidates
    {
        public AzureSearchAdminKeyRekeyableObjectProvider(ProviderWorkflowActionLogger<AzureSearchAdminKeyRekeyableObjectProvider> logger) : base(logger) { }

        protected override string Service => "Azure Search";

        protected override AdminKeyKind Translate(AzureSearchAdminKeyConfiguration.AzureSearchKeyKinds keyType) => keyType switch
        {
            AzureSearchAdminKeyConfiguration.AzureSearchKeyKinds.Primary => AdminKeyKind.Primary,
            AzureSearchAdminKeyConfiguration.AzureSearchKeyKinds.Secondary => AdminKeyKind.Secondary,
            _ => throw new NotImplementedException()
        };

        protected override Task<IAdminKeys> RetrieveCurrentKeyring(ISearchService resource, AdminKeyKind keyType) => resource.GetAdminKeysAsync();

        protected override Task<IAdminKeys> RotateKeyringValue(ISearchService resource, AdminKeyKind keyType) => resource.RegenerateAdminKeysAsync(keyType);

        protected override RegeneratedSecret CreateSecretFromKeyring(IAdminKeys keyring, AzureSearchAdminKeyConfiguration.AzureSearchKeyKinds keyType) =>
            new RegeneratedSecret()
            {
                NewSecretValue = (keyType switch
                {
                    AzureSearchAdminKeyConfiguration.AzureSearchKeyKinds.Primary => keyring.PrimaryKey,
                    AzureSearchAdminKeyConfiguration.AzureSearchKeyKinds.Secondary => keyring.SecondaryKey,
                    _ => throw new NotImplementedException(),
                }).GetSecureString()
            };

        protected override ISupportsGettingByResourceGroup<ISearchService> GetResourceCollection(IAzure azure) => azure.SearchServices;

        public async Task<List<ProviderResourceSuggestion>> EnumerateResourceCandidates(AuthJanitorProviderConfiguration baseConfig)
        {
            var azureConfig = baseConfig as AzureAuthJanitorProviderConfiguration;

            IPagedCollection<ISearchService> items;
            if (!string.IsNullOrEmpty(azureConfig.ResourceGroup))
                items = await (await GetAzureAsync()).SearchServices.ListByResourceGroupAsync(azureConfig.ResourceGroup);
            else
                items = await (await GetAzureAsync()).SearchServices.ListAsync();

            return items.Select(i => new ProviderResourceSuggestion()
            {
                Configuration = new AzureSearchAdminKeyConfiguration()
                {
                    ResourceGroup = i.ResourceGroupName,
                    ResourceName = i.Name,
                    KeyType = AzureSearchAdminKeyConfiguration.AzureSearchKeyKinds.Primary
                },
                Name = $"Azure Search - {i.ResourceGroupName} - {i.Name}",
                ProviderType = this.GetType().AssemblyQualifiedName,
                AddressableNames = new[] { i.Name }
            }).ToList();
        }
    }
}
