// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers.Azure.Workflows;
using Microsoft.Azure.Management.CosmosDB.Fluent;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core.CollectionActions;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.CosmosDb
{
    [Provider(Name = "CosmosDB Master Key",
              IconClass = "fa fa-database",
              Description = "Regenerates a Master Key for an Azure CosmosDB instance",
              Features = ProviderFeatureFlags.CanRotateWithoutDowntime |
                         ProviderFeatureFlags.IsTestable |
                         ProviderFeatureFlags.SupportsSecondaryKey)]
    [ProviderImage(ProviderImages.COSMOS_DB_SVG)]
    public class CosmosDbRekeyableObjectProvider : TwoKeyAzureRekeyableObjectProvider<CosmosDbKeyConfiguration, ICosmosDBAccount, IDatabaseAccountListKeysResult, CosmosDbKeyConfiguration.CosmosDbKeyKinds, string>
    {
        private const string PRIMARY_READONLY_KEY = "primaryReadOnly";
        private const string SECONDARY_READONLY_KEY = "secondaryReadOnly";
        private const string PRIMARY_KEY = "primary";
        private const string SECONDARY_KEY = "secondary";

        public CosmosDbRekeyableObjectProvider(ILogger<CosmosDbRekeyableObjectProvider> logger) : base(logger) { }

        protected override string Service => "CosmosDB";

        protected override RegeneratedSecret CreateSecretFromKeyring(IDatabaseAccountListKeysResult keyring, CosmosDbKeyConfiguration.CosmosDbKeyKinds keyType) =>
            new RegeneratedSecret()
            {
                NewSecretValue = keyType switch
                {
                    CosmosDbKeyConfiguration.CosmosDbKeyKinds.Primary => keyring.PrimaryMasterKey,
                    CosmosDbKeyConfiguration.CosmosDbKeyKinds.Secondary => keyring.SecondaryMasterKey,
                    CosmosDbKeyConfiguration.CosmosDbKeyKinds.PrimaryReadOnly => keyring.PrimaryReadonlyMasterKey,
                    CosmosDbKeyConfiguration.CosmosDbKeyKinds.SecondaryReadOnly => keyring.SecondaryReadonlyMasterKey,
                    _ => throw new NotImplementedException()
                }
            };

        protected override ISupportsGettingByResourceGroup<ICosmosDBAccount> GetResourceCollection(IAzure azure) => azure.CosmosDBAccounts;
        protected override Task<IDatabaseAccountListKeysResult> RetrieveCurrentKeyring(ICosmosDBAccount resource, string keyType) => resource.ListKeysAsync();
        protected override async Task<IDatabaseAccountListKeysResult> RotateKeyringValue(ICosmosDBAccount resource, string keyType) { await resource.RegenerateKeyAsync(keyType); return null; }

        protected override string Translate(CosmosDbKeyConfiguration.CosmosDbKeyKinds keyType) => keyType switch
        {
            CosmosDbKeyConfiguration.CosmosDbKeyKinds.Primary => PRIMARY_KEY,
            CosmosDbKeyConfiguration.CosmosDbKeyKinds.Secondary => SECONDARY_KEY,
            CosmosDbKeyConfiguration.CosmosDbKeyKinds.PrimaryReadOnly => PRIMARY_READONLY_KEY,
            CosmosDbKeyConfiguration.CosmosDbKeyKinds.SecondaryReadOnly => SECONDARY_READONLY_KEY,
            _ => throw new NotImplementedException(),
        };
    }
}