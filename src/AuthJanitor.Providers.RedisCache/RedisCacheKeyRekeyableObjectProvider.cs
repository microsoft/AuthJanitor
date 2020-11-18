// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Integrations.CryptographicImplementations;
using AuthJanitor.Providers.Azure;
using AuthJanitor.Providers.Azure.Workflows;
using AuthJanitor.Providers.Capabilities;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Redis.Fluent;
using Microsoft.Azure.Management.Redis.Fluent.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core.CollectionActions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.Redis
{
    [Provider(Name = "Redis Cache Key",
              Description = "Regenerates a Master Key for a Redis Cache instance",
              SvgImage = ProviderImages.REDIS_SVG)]
    public class RedisCacheKeyRekeyableObjectProvider : TwoKeyAzureRekeyableObjectProvider<RedisCacheKeyConfiguration, IRedisCache, IRedisAccessKeys, RedisCacheKeyConfiguration.RedisKeyTypes, RedisKeyType>,
        ICanEnumerateResourceCandidates
    {
        protected override string Service => "Redis Cache";

        public RedisCacheKeyRekeyableObjectProvider(ProviderWorkflowActionLogger<RedisCacheKeyRekeyableObjectProvider> logger) : base(logger) { }

        protected override RegeneratedSecret CreateSecretFromKeyring(IRedisAccessKeys keyring, RedisCacheKeyConfiguration.RedisKeyTypes keyType) =>
            new RegeneratedSecret()
            {
                NewSecretValue = (keyType switch
                {
                    RedisCacheKeyConfiguration.RedisKeyTypes.Primary => keyring.PrimaryKey,
                    RedisCacheKeyConfiguration.RedisKeyTypes.Secondary => keyring.SecondaryKey,
                    _ => throw new NotImplementedException(),
                }).GetSecureString()
            };

        protected override ISupportsGettingByResourceGroup<IRedisCache> GetResourceCollection(IAzure azure) => azure.RedisCaches;

        // NOTE: Redis Fluent lacks "GetKeysAsync"
        protected override Task<IRedisAccessKeys> RetrieveCurrentKeyring(IRedisCache resource, RedisKeyType keyType) => Task.FromResult(resource.GetKeys());

        // NOTE: Redis Fluent lacks "RegenerateKeyAsync"
        protected override Task<IRedisAccessKeys> RotateKeyringValue(IRedisCache resource, RedisKeyType keyType) => Task.FromResult(resource.RegenerateKey(keyType));

        protected override RedisKeyType Translate(RedisCacheKeyConfiguration.RedisKeyTypes keyType) => keyType switch
        {
            RedisCacheKeyConfiguration.RedisKeyTypes.Primary => RedisKeyType.Primary,
            RedisCacheKeyConfiguration.RedisKeyTypes.Secondary => RedisKeyType.Secondary,
            _ => throw new NotImplementedException()
        };

        public async Task<List<ProviderResourceSuggestion>> EnumerateResourceCandidates(AuthJanitorProviderConfiguration baseConfig)
        {
            var azureConfig = baseConfig as AzureAuthJanitorProviderConfiguration;

            IPagedCollection<IRedisCache> items;
            if (!string.IsNullOrEmpty(azureConfig.ResourceGroup))
                items = await (await GetAzureAsync()).RedisCaches.ListByResourceGroupAsync(azureConfig.ResourceGroup);
            else
                items = await (await GetAzureAsync()).RedisCaches.ListAsync();

            return items.Select(i =>
            new ProviderResourceSuggestion()
            {
                Configuration = new RedisCacheKeyConfiguration()
                {
                    ResourceGroup = i.ResourceGroupName,
                    ResourceName = i.Name,
                    KeyType = RedisCacheKeyConfiguration.RedisKeyTypes.Primary
                },
                Name = $"Redis Cache - {i.ResourceGroupName} - {i.Name}",
                ProviderType = this.GetType().AssemblyQualifiedName,
                AddressableNames = new[] { i.HostName }
            }).ToList();
        }
    }
}