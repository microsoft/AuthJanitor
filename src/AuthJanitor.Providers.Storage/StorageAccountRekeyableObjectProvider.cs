// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Integrations.CryptographicImplementations;
using AuthJanitor.Providers.Azure;
using AuthJanitor.Providers.Azure.Workflows;
using AuthJanitor.Providers.Capabilities;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
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
              Description = "Regenerates a key of a specified type for an Azure Storage Account",
              SvgImage = ProviderImages.STORAGE_ACCOUNT_SVG)]
    public class StorageAccountRekeyableObjectProvider : TwoKeyAzureRekeyableObjectProvider<StorageAccountKeyConfiguration, IStorageAccount, IReadOnlyList<StorageAccountKey>, StorageAccountKeyConfiguration.StorageKeyTypes, string>,
        ICanEnumerateResourceCandidates
    {
        private const string KEY1 = "key1";
        private const string KEY2 = "key2";
        private const string KERB1 = "kerb1";
        private const string KERB2 = "kerb2";

        public StorageAccountRekeyableObjectProvider(ILogger<StorageAccountRekeyableObjectProvider> logger) : base(logger) { }

        protected override string Service => "Storage";

        public async Task<List<ProviderResourceSuggestion>> EnumerateResourceCandidates(AuthJanitorProviderConfiguration baseConfig)
        {
            var azureConfig = baseConfig as AzureAuthJanitorProviderConfiguration;

            IPagedCollection<IStorageAccount> items;
            if (!string.IsNullOrEmpty(azureConfig.ResourceGroup))
                items = await (await GetAzureAsync()).StorageAccounts.ListByResourceGroupAsync(azureConfig.ResourceGroup);
            else
                items = await (await GetAzureAsync()).StorageAccounts.ListAsync();

            return items.Select(i =>
            {
                return new ProviderResourceSuggestion()
                {
                    Configuration = new StorageAccountKeyConfiguration()
                    {
                        ResourceGroup = i.ResourceGroupName,
                        ResourceName = i.Name
                    },
                    Name = $"Storage Account - {i.ResourceGroupName} - {i.Name}",
                    ProviderType = this.GetType().AssemblyQualifiedName,
                    AddressableNames = new List<string>()
                    {
                        i.Name,
                        i.EndPoints?.Primary?.Blob,
                        i.EndPoints?.Primary?.Dfs,
                        i.EndPoints?.Primary?.File,
                        i.EndPoints?.Primary?.Queue,
                        i.EndPoints?.Primary?.Table,
                        i.EndPoints?.Primary?.Web,
                        i.EndPoints?.Secondary?.Blob,
                        i.EndPoints?.Secondary?.Dfs,
                        i.EndPoints?.Secondary?.File,
                        i.EndPoints?.Secondary?.Queue,
                        i.EndPoints?.Secondary?.Table,
                        i.EndPoints?.Secondary?.Web,
                    }.Where(a => !string.IsNullOrEmpty(a))
                };
            }).ToList();
        }

        protected override RegeneratedSecret CreateSecretFromKeyring(IReadOnlyList<StorageAccountKey> keyring, StorageAccountKeyConfiguration.StorageKeyTypes keyType)
        {
            var key = keyring.First(k => k.KeyName == Translate(keyType)).Value;
            return new RegeneratedSecret()
            {
                NewSecretValue = key.GetSecureString(),
                NewConnectionString = $"DefaultEndpointsProtocol=https;AccountName={Configuration.ResourceName};AccountKey={key};EndpointSuffix=core.windows.net".GetSecureString()
            };
        }

        protected override ISupportsGettingByResourceGroup<IStorageAccount> GetResourceCollection(IAzure azure) => azure.StorageAccounts;

        protected override Task<IReadOnlyList<StorageAccountKey>> RetrieveCurrentKeyring(IStorageAccount resource, string keyType) => resource.GetKeysAsync();

        protected override async Task<IReadOnlyList<StorageAccountKey>> RotateKeyringValue(IStorageAccount resource, string keyType)
        {
            var result = await resource.RegenerateKeyAsync(keyType);
            var expectedNewKey = result.FirstOrDefault(k => k.KeyName == keyType);
            var end = DateTime.Now.AddMinutes(2);
            while (DateTime.Now < end)
            {
                var keyring = await RetrieveCurrentKeyring(resource, keyType);
                var key = keyring.FirstOrDefault(k => k.KeyName == keyType);
                if (key == null) continue;
                if (key.Value == expectedNewKey.Value) return result;
            }
            throw new Exception("Storage key was reported as rotated, but didn't resync within 2 minutes!");
        }

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
