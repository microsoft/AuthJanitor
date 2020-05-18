// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Extensions.Azure;
using Microsoft.Azure.Management.Redis.Fluent;
using Microsoft.Azure.Management.Redis.Fluent.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.Redis
{
    [Provider(Name = "Redis Cache Key",
              IconClass = "fa fa-database",
              Description = "Regenerates a Master Key for a Redis Cache instance")]
    [ProviderImage(ProviderImages.REDIS_SVG)]
    public class RedisCacheKeyRekeyableObjectProvider : RekeyableObjectProvider<RedisCacheKeyConfiguration>
    {
        private readonly ILogger _logger;

        public RedisCacheKeyRekeyableObjectProvider(ILogger<RedisCacheKeyRekeyableObjectProvider> logger)
        {
            _logger = logger;
        }

        public override async Task<RegeneratedSecret> GetSecretToUseDuringRekeying()
        {
            // NOTE: Redis Fluent lacks async methods
            _logger.LogInformation("Getting temporary secret to use during rekeying from other ({OtherKeyType}) key...", GetOtherKeyType(Configuration.KeyType));
            var redisCache = await RedisCache;
            IRedisAccessKeys keys = redisCache.GetKeys();
            _logger.LogInformation("Successfully retrieved temporary secret!");
            return new RegeneratedSecret()
            {
                Expiry = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(10),
                UserHint = Configuration.UserHint,
                NewSecretValue = GetKeyValue(keys, GetOtherKeyType(Configuration.KeyType))
            };
        }

        public override async Task<RegeneratedSecret> Rekey(TimeSpan requestedValidPeriod)
        {
            // NOTE: Redis Fluent lacks async methods
            _logger.LogInformation("Regenerating Redis Cache key type {KeyType}", Configuration.KeyType);
            var redisCache = await RedisCache;
            var keys = redisCache.RegenerateKey(GetKeyType(Configuration.KeyType));

            _logger.LogInformation("Successfully rekeyed Redis Cache key type {KeyType}", Configuration.KeyType);
            return new RegeneratedSecret()
            {
                Expiry = DateTimeOffset.UtcNow + requestedValidPeriod,
                UserHint = Configuration.UserHint,
                NewSecretValue = GetKeyValue(keys, GetKeyType(Configuration.KeyType))
            };
        }

        public override async Task OnConsumingApplicationSwapped()
        {
            // NOTE: Redis Fluent lacks async methods
            if (!Configuration.SkipScramblingOtherKey)
            {
                _logger.LogInformation("Scrambling Redis Cache key kind {OtherKeyType}", GetOtherKeyType(Configuration.KeyType));
                (await RedisCache).RegenerateKey(GetOtherKeyType(Configuration.KeyType));
            }
            else
                _logger.LogInformation("Skipping scrambling CosmosDB key kind {OtherKeyType}", GetOtherKeyType(Configuration.KeyType));
        }

        public override IList<RiskyConfigurationItem> GetRisks()
        {
            List<RiskyConfigurationItem> issues = new List<RiskyConfigurationItem>();
            if (Configuration.SkipScramblingOtherKey)
            {
                issues.Add(new RiskyConfigurationItem()
                {
                    Score = 80,
                    Risk = $"The other (unused) Redis Cache Key of this type is not being scrambled during key rotation",
                    Recommendation = "Unless other services use the alternate key, consider allowing the scrambling of the unused key to 'fully' rekey Redis Cache and maintain a high degree of security."
                });
            }

            return issues;
        }

        public override string GetDescription() =>
            $"Regenerates the {Configuration.KeyType} key for a Redis Cache instance " +
            $"called '{ResourceName}' (Resource Group '{ResourceGroup}'). " +
            $"The {GetOtherKeyType(Configuration.KeyType)} key is used as a temporary " +
            $"key while rekeying is taking place. The {GetOtherKeyType(Configuration.KeyType)} " +
            $"key will {(Configuration.SkipScramblingOtherKey ? "not" : "also")} be rotated.";

        private Task<IRedisCache> RedisCache => this.GetAzure().ContinueWith(az => az.Result.RedisCaches.GetByResourceGroupAsync(ResourceGroup, ResourceName)).Unwrap();

        private RedisKeyType GetKeyType(RedisCacheKeyConfiguration.RedisKeyTypes keyType) => keyType switch
        {
            RedisCacheKeyConfiguration.RedisKeyTypes.Primary => RedisKeyType.Primary,
            RedisCacheKeyConfiguration.RedisKeyTypes.Secondary => RedisKeyType.Secondary,
            _ => throw new System.Exception($"Key type '{keyType}' not implemented")
        };

        private RedisKeyType GetOtherKeyType(RedisCacheKeyConfiguration.RedisKeyTypes keyType) => keyType switch
        {
            RedisCacheKeyConfiguration.RedisKeyTypes.Primary => RedisKeyType.Secondary,
            RedisCacheKeyConfiguration.RedisKeyTypes.Secondary => RedisKeyType.Primary,
            _ => throw new System.Exception($"Key type '{keyType}' not implemented")
        };

        private string GetKeyValue(IRedisAccessKeys keys, RedisKeyType keyType) => keyType switch
        {
            RedisKeyType.Primary => keys.PrimaryKey,
            RedisKeyType.Secondary => keys.SecondaryKey,
            _ => throw new System.Exception($"Key type '{keyType}' not implemented")
        };
    }
}