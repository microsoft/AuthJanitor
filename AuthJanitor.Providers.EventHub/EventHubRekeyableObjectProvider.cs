// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Extensions.Azure;
using AuthJanitor.Integrations.CryptographicImplementations;
using Microsoft.Azure.Management.Eventhub.Fluent;
using Microsoft.Azure.Management.EventHub.Fluent.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.EventHub
{
    [Provider(Name = "Event Hub Key",
              IconClass = "fa fa-key",
              Description = "Regenerates an Azure Event Hub Key")]
    [ProviderImage(ProviderImages.EVENT_HUB_SVG)]
    public class EventHubRekeyableObjectProvider : RekeyableObjectProvider<EventHubKeyConfiguration>
    {
        private readonly ILogger _logger;

        public EventHubRekeyableObjectProvider(ILogger<EventHubRekeyableObjectProvider> logger)
        {
            _logger = logger;
        }

        public override async Task<RegeneratedSecret> GetSecretToUseDuringRekeying()
        {
            _logger.LogInformation("Getting temporary secret to use during rekeying from other ({OtherKeyType}) policy key...", OtherKeyType);
            var keys = await (await GetAuthorizationRule()).GetKeysAsync();
            _logger.LogInformation("Successfully retrieved temporary secret!");

            return new RegeneratedSecret()
            {
                Expiry = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(10),
                UserHint = Configuration.UserHint,
                NewSecretValue = GetKeyValue(keys, OtherKeyType),
                NewConnectionString = GetConnectionStringValue(keys, OtherKeyType)
            };
        }

        public override async Task<RegeneratedSecret> Rekey(TimeSpan requestedValidPeriod)
        {
            _logger.LogInformation("Regenerating Event Hub key {KeyType}", KeyType);
            var keys = await (await GetAuthorizationRule()).RegenerateKeyAsync(this.KeyType);
            _logger.LogInformation("Successfully rekeyed Event Hub key {KeyType}", KeyType);
            return new RegeneratedSecret()
            {
                Expiry = DateTimeOffset.UtcNow + requestedValidPeriod,
                UserHint = Configuration.UserHint,
                NewSecretValue = GetKeyValue(keys, KeyType),
                NewConnectionString = GetConnectionStringValue(keys, KeyType)
            };
        }

        public override async Task OnConsumingApplicationSwapped()
        {
            if (!Configuration.SkipScramblingOtherKey)
            {
                _logger.LogInformation("Scrambling Event Hub key kind {OtherKeyType}", OtherKeyType);
                await (await GetAuthorizationRule()).RegenerateKeyAsync(this.OtherKeyType);
            }
            else
                _logger.LogInformation("Skipping scrambling Event Hub key kind {OtherKeyType}", OtherKeyType);
        }

        public override IList<RiskyConfigurationItem> GetRisks()
        {
            List<RiskyConfigurationItem> issues = new List<RiskyConfigurationItem>();
            if (Configuration.SkipScramblingOtherKey)
            {
                issues.Add(new RiskyConfigurationItem()
                {
                    Score = 80,
                    Risk = $"The other (unused) Event Hub Key is not being scrambled during key rotation",
                    Recommendation = "Unless other services use the alternate key, consider allowing the scrambling of the unused key to 'fully' rekey the Event Hub and maintain a high degree of security."
                });
            }

            return issues;
        }

        public override string GetDescription() =>
            $"Regenerates the {KeyType} key for an Event Hub called " +
            $"'{ResourceName}' in namespace '{Configuration.NamespaceName}' (Resource Group '{ResourceGroup}') for the " +
            $"authorization rule {Configuration.AuthorizationRuleName}. " +
            $"The {OtherKeyType} key is used as a temporary key while " +
            $"rekeying is taking place. The {OtherKeyType} key will " +
            $"{(Configuration.SkipScramblingOtherKey ? "not" : "also")} be rotated.";

        private KeyType KeyType =>
            Configuration.KeyType switch
            {
                EventHubKeyConfiguration.EventHubKeyTypes.Secondary => KeyType.SecondaryKey,
                _ => KeyType.PrimaryKey,
            };
        private KeyType OtherKeyType =>
            Configuration.KeyType switch
            {
                EventHubKeyConfiguration.EventHubKeyTypes.Secondary => KeyType.PrimaryKey,
                _ => KeyType.SecondaryKey,
            };

        private Task<IEventHub> EventHub => this.GetAzure().ContinueWith(az => az.Result.EventHubs.GetByNameAsync(ResourceGroup, Configuration.NamespaceName, ResourceName)).Unwrap();
        private async Task<IEventHubAuthorizationRule> GetAuthorizationRule()
        {
            var eventHub = await EventHub;
            var authorizationRules = await eventHub.ListAuthorizationRulesAsync();
            return authorizationRules.FirstOrDefault(r => r.Name == Configuration.AuthorizationRuleName);
        }

        private SecureString GetKeyValue(IEventHubAuthorizationKey keys, KeyType type) => type switch
        {
            KeyType.SecondaryKey => keys.SecondaryKey.GetSecureString(),
            _ => keys.PrimaryKey.GetSecureString(),
        };

        private SecureString GetConnectionStringValue(IEventHubAuthorizationKey keys, KeyType type) => type switch
        {
            KeyType.SecondaryKey => keys.SecondaryConnectionString.GetSecureString(),
            _ => keys.PrimaryConnectionString.GetSecureString(),
        };
    }
}