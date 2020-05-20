// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers.Azure.Workflows;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core.CollectionActions;
using Microsoft.Azure.Management.Search.Fluent;
using Microsoft.Azure.Management.Search.Fluent.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.AzureSearch
{
    [Provider(Name = "Azure Search Admin Key",
              IconClass = "fas fa-search",
              Description = "Regenerates an Admin Key for an Azure Search service",
              Features = ProviderFeatureFlags.CanRotateWithoutDowntime |
                         ProviderFeatureFlags.IsTestable |
                         ProviderFeatureFlags.SupportsSecondaryKey)]
    [ProviderImage(ProviderImages.AZURE_SEARCH_SVG)]
    public class AzureSearchAdminKeyRekeyableObjectProvider : TwoKeyAzureRekeyableObjectProvider<AzureSearchAdminKeyConfiguration, ISearchService, IAdminKeys, AzureSearchAdminKeyConfiguration.AzureSearchKeyKinds, AdminKeyKind>
    {
        public AzureSearchAdminKeyRekeyableObjectProvider(ILogger<AzureSearchAdminKeyRekeyableObjectProvider> logger) : base(logger) { }

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
                NewSecretValue = keyType switch
                {
                    AzureSearchAdminKeyConfiguration.AzureSearchKeyKinds.Primary => keyring.PrimaryKey,
                    AzureSearchAdminKeyConfiguration.AzureSearchKeyKinds.Secondary => keyring.SecondaryKey,
                    _ => throw new NotImplementedException(),
                }
            };

        protected override ISupportsGettingByResourceGroup<ISearchService> GetResourceCollection(IAzure azure) => azure.SearchServices;
    }
}
