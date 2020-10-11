// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.IdentityServices;
using AuthJanitor.Integrations.CryptographicImplementations;
using AuthJanitor.Providers.Azure;
using AuthJanitor.Providers.Capabilities;
using Microsoft.Azure.Management.Storage.Fluent;
using Microsoft.Azure.Management.Storage.Fluent.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.Storage
{
    public interface IProviderFactory
    {
        Task<List<ProviderResourceCandidate>> GetProviders();
        Task GetProviders(List<ProviderResourceCandidate> providers);
    }
    public class StorageAccountProviderFactory : IProviderFactory
    {
        private readonly ProviderManagerService _providerManagerService;
        private readonly IIdentityService _identityService;
        private readonly ICryptographicImplementation _cryptography;

        public StorageAccountProviderFactory(
            ILogger<StorageAccountProviderFactory> logger,
            IIdentityService identityService,
            ICryptographicImplementation cryptography,
            ProviderManagerService providerManagerService)
        {
            _identityService = identityService;
            _cryptography = cryptography;
            _providerManagerService = providerManagerService;
        }

        private string TargetProvider => typeof(StorageAccountRekeyableObjectProvider).AssemblyQualifiedName;
        public async Task<List<ProviderResourceCandidate>> GetProviders()
        {
            var candidates = new List<ProviderResourceCandidate>();
            var token = await _identityService.GetAccessTokenOnBehalfOfCurrentUserAsync();
            var azure = await Microsoft.Azure.Management.Fluent.Azure
                    .Configure()
                    .Authenticate(token.CreateAzureCredentials())
                    .WithDefaultSubscriptionAsync();

            var storageAccounts = await azure.StorageAccounts.ListAsync();
            foreach (var storageAccount in storageAccounts)
            {
                var keys = await storageAccount.GetKeysAsync();
                candidates.AddRange(new[]
                {
                    await CreateFromStorageAccount(storageAccount, keys, StorageAccountKeyConfiguration.StorageKeyTypes.Key1),
                    await CreateFromStorageAccount(storageAccount, keys, StorageAccountKeyConfiguration.StorageKeyTypes.Key2),
                    await CreateFromStorageAccount(storageAccount, keys, StorageAccountKeyConfiguration.StorageKeyTypes.Kerb1),
                    await CreateFromStorageAccount(storageAccount, keys, StorageAccountKeyConfiguration.StorageKeyTypes.Kerb2)
                });
            }

            return candidates;
        }

        private async Task<ProviderResourceCandidate> CreateFromStorageAccount(IStorageAccount storageAccount, IReadOnlyList<StorageAccountKey> keys, StorageAccountKeyConfiguration.StorageKeyTypes keyType)
        {
            var providerTemplate = _providerManagerService.GetProviderInstance(TargetProvider) as StorageAccountRekeyableObjectProvider;
            providerTemplate.Configuration.ResourceName = storageAccount.Name;
            providerTemplate.Configuration.ResourceGroup = storageAccount.ResourceGroupName;
            providerTemplate.Configuration.KeyType = keyType;

            var value = keys.First(k => k.KeyName.ToUpper() == keyType.ToString().ToUpper()).Value;
            return new ProviderResourceCandidate()
            {
                RekeyableObjectProvider = providerTemplate,
                RekeyableObjectIdentifiers = new HashSet<string>()
                {
                    storageAccount.EndPoints.Primary.Blob,
                    storageAccount.EndPoints.Primary.Dfs,
                    storageAccount.EndPoints.Primary.File,
                    storageAccount.EndPoints.Primary.Queue,
                    storageAccount.EndPoints.Primary.Table,
                    storageAccount.EndPoints.Primary.Web,
                    storageAccount.EndPoints.Secondary.Blob,
                    storageAccount.EndPoints.Secondary.Dfs,
                    storageAccount.EndPoints.Secondary.File,
                    storageAccount.EndPoints.Secondary.Queue,
                    storageAccount.EndPoints.Secondary.Table,
                    storageAccount.EndPoints.Secondary.Web,
                },
                RekeyableObjectSecretHashes = new HashSet<string>()
                {
                    await _cryptography.Hash(value),
                    await _cryptography.Hash($"DefaultEndpointsProtocol=https;AccountName={storageAccount.Name};AccountKey={value};EndpointSuffix=core.windows.net")
                }
            };
        }

        public Task GetProviders(List<ProviderResourceCandidate> providers) => Task.FromResult(0);
    }
}
