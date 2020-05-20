// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers.Azure.Workflows;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Redis.Fluent;
using Microsoft.Azure.Management.Redis.Fluent.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core.CollectionActions;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.Redis
{
    [Provider(Name = "Redis Cache Key",
              IconClass = "fa fa-database",
              Description = "Regenerates a Master Key for a Redis Cache instance",
              Features = ProviderFeatureFlags.CanRotateWithoutDowntime |
                         ProviderFeatureFlags.IsTestable |
                         ProviderFeatureFlags.SupportsSecondaryKey)]
    [ProviderImage(ProviderImages.REDIS_SVG)]
    public class RedisCacheKeyRekeyableObjectProvider : TwoKeyAzureRekeyableObjectProvider<RedisCacheKeyConfiguration, IRedisCache, IRedisAccessKeys, RedisCacheKeyConfiguration.RedisKeyTypes, RedisKeyType>
    {
        protected override string Service => "Redis Cache";

        public RedisCacheKeyRekeyableObjectProvider(ILogger<RedisCacheKeyRekeyableObjectProvider> logger) : base(logger) { }

        protected override RegeneratedSecret CreateSecretFromKeyring(IRedisAccessKeys keyring, RedisCacheKeyConfiguration.RedisKeyTypes keyType) =>
            new RegeneratedSecret()
            {
                NewSecretValue = keyType switch
                {
                    RedisCacheKeyConfiguration.RedisKeyTypes.Primary => keyring.PrimaryKey,
                    RedisCacheKeyConfiguration.RedisKeyTypes.Secondary => keyring.SecondaryKey,
                    _ => throw new NotImplementedException(),
                }
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
    }
}