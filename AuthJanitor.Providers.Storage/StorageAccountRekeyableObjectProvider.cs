// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers.Azure.Workflows;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core.CollectionActions;
using Microsoft.Azure.Management.Storage.Fluent;
using Microsoft.Azure.Management.Storage.Fluent.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.Storage
{
    [Provider(Name = "Storage Account Key",
              IconClass = "fas fa-file-alt",
              Description = "Regenerates a key of a specified type for an Azure Storage Account",
              Features = ProviderFeatureFlags.CanRotateWithoutDowntime | 
                         ProviderFeatureFlags.IsTestable |
                         ProviderFeatureFlags.SupportsSecondaryKey)]
    [ProviderImage(ProviderImages.STORAGE_ACCOUNT_SVG)]
    public class StorageAccountRekeyableObjectProvider : TwoKeyAzureRekeyableObjectProvider<StorageAccountKeyConfiguration, IStorageAccount, IReadOnlyList<StorageAccountKey>, StorageAccountKeyConfiguration.StorageKeyTypes, string>
    {
        private const string KEY1 = "key1";
        private const string KEY2 = "key2";
        private const string KERB1 = "kerb1";
        private const string KERB2 = "kerb2";

        public StorageAccountRekeyableObjectProvider(ILogger<StorageAccountRekeyableObjectProvider> logger) : base(logger) { }

        protected override string Service => "Storage";

        protected override RegeneratedSecret CreateSecretFromKeyring(IReadOnlyList<StorageAccountKey> keyring, StorageAccountKeyConfiguration.StorageKeyTypes keyType)
        {
            var key = keyring.First(k => k.KeyName == Translate(keyType)).Value;
            return new RegeneratedSecret()
            {
                NewSecretValue = key,
                NewConnectionString = $"DefaultEndpointsProtocol=https;AccountName={Configuration.ResourceName};AccountKey={key};EndpointSuffix=core.windows.net"
            };
        }

        protected override ISupportsGettingByResourceGroup<IStorageAccount> GetResourceCollection(IAzure azure) => azure.StorageAccounts;

        protected override Task<IReadOnlyList<StorageAccountKey>> RetrieveCurrentKeyring(IStorageAccount resource, string keyType) => resource.GetKeysAsync();

        protected override Task<IReadOnlyList<StorageAccountKey>> RotateKeyringValue(IStorageAccount resource, string keyType) => resource.RegenerateKeyAsync(keyType);

        protected override string Translate(StorageAccountKeyConfiguration.StorageKeyTypes keyType) => keyType switch
        {
            StorageAccountKeyConfiguration.StorageKeyTypes.Key1 => KEY1,
            StorageAccountKeyConfiguration.StorageKeyTypes.Key2 => KEY2,
            StorageAccountKeyConfiguration.StorageKeyTypes.Kerb1 => KERB1,
            StorageAccountKeyConfiguration.StorageKeyTypes.Kerb2 => KERB2,
            _ => throw new NotImplementedException()
        };
    }
}
