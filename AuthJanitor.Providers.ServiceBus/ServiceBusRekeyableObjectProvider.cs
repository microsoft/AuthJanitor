// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Microsoft.Azure.Management.ServiceBus.Fluent;
using Microsoft.Azure.Management.ServiceBus.Fluent.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.ServiceBus
{
    [Provider(Name = "Service Bus Key",
              IconClass = "fa fa-key",
              Description = "Regenerates an Azure Service Bus Key")]
    [ProviderImage(ProviderImages.SERVICE_BUS_SVG)]
    public class ServiceBusRekeyableObjectProvider : RekeyableObjectProvider<ServiceBusKeyConfiguration>
    {
        public ServiceBusRekeyableObjectProvider(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        public override async Task<RegeneratedSecret> GetSecretToUseDuringRekeying()
        {
            Logger.LogInformation("Getting temporary secret to use during rekeying from other ({0}) policy key...", OtherPolicyKey);
            IAuthorizationKeys otherKeys = await Get();
            Logger.LogInformation("Successfully retrieved temporary secret!");

            return new RegeneratedSecret()
            {
                Expiry = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(10),
                UserHint = Configuration.UserHint,
                NewSecretValue = GetKeyValue(OtherPolicyKey, otherKeys),
                NewConnectionString = GetConnectionStringValue(OtherPolicyKey, otherKeys)
            };
        }

        public override async Task<RegeneratedSecret> Rekey(TimeSpan requestedValidPeriod)
        {
            Logger.LogInformation("Regenerating Service Bus key {0}", PolicyKey);
            IAuthorizationKeys newKeys = await Regenerate(PolicyKey);
            Logger.LogInformation("Successfully rekeyed Service Bus key {0}", PolicyKey);
            return new RegeneratedSecret()
            {
                Expiry = DateTimeOffset.UtcNow + requestedValidPeriod,
                UserHint = Configuration.UserHint,
                NewSecretValue = GetKeyValue(PolicyKey, newKeys),
                NewConnectionString = GetConnectionStringValue(PolicyKey, newKeys)
            };
        }

        public override async Task OnConsumingApplicationSwapped()
        {
            if (!Configuration.SkipScramblingOtherKey)
            {
                Logger.LogInformation("Scrambling Service Bus key kind {0}", OtherPolicyKey);
                await Regenerate(OtherPolicyKey);
            }
            else
                Logger.LogInformation("Skipping scrambling Service Bus key kind {0}", OtherPolicyKey);
        }

        public override IList<RiskyConfigurationItem> GetRisks()
        {
            List<RiskyConfigurationItem> issues = new List<RiskyConfigurationItem>();
            if (Configuration.SkipScramblingOtherKey)
            {
                issues.Add(new RiskyConfigurationItem()
                {
                    Score = 80,
                    Risk = $"The other (unused) Service Bus Key is not being scrambled during key rotation",
                    Recommendation = "Unless other services use the alternate key, consider allowing the scrambling of the unused key to 'fully' rekey the Service Bus and maintain a high degree of security."
                });
            }

            return issues;
        }

        public override string GetDescription() =>
            $"Regenerates the {PolicyKey} key for a Service Bus called " +
            $"'{ResourceName}' (Resource Group '{ResourceGroup}') for the " +
            $"authorization rule {Configuration.AuthorizationRuleName}. " +
            $"The {OtherPolicyKey} key is used as a temporary key while " +
            $"rekeying is taking place. The {OtherPolicyKey} key will " +
            $"{(Configuration.SkipScramblingOtherKey ? "not" : "also")} be rotated.";

        private Policykey PolicyKey =>
            Configuration.KeyType switch
            {
                ServiceBusKeyConfiguration.ServiceBusKeyTypes.Secondary => Policykey.SecondaryKey,
                _ => Policykey.PrimaryKey,
            };
        private Policykey OtherPolicyKey =>
            Configuration.KeyType switch
            {
                ServiceBusKeyConfiguration.ServiceBusKeyTypes.Secondary => Policykey.PrimaryKey,
                _ => Policykey.SecondaryKey,
            };

        private Task<IAuthorizationKeys> Regenerate(Policykey keyType) =>
            AuthorizationRule.ContinueWith(rule => rule.Result.RegenerateKeyAsync(keyType)).Unwrap();
        private Task<IAuthorizationKeys> Get() =>
            AuthorizationRule.ContinueWith(rule => rule.Result.GetKeysAsync()).Unwrap();

        private Task<IServiceBusNamespace> ServiceBusNamespace => GetAzure().ContinueWith(az => az.Result.ServiceBusNamespaces.GetByResourceGroupAsync(ResourceGroup, ResourceName)).Unwrap();
        private Task<INamespaceAuthorizationRule> AuthorizationRule => ServiceBusNamespace.ContinueWith(ns => ns.Result.AuthorizationRules.GetByNameAsync(Configuration.AuthorizationRuleName)).Unwrap();

        private string GetKeyValue(Policykey key, IAuthorizationKeys keys) => key switch
        {
            Policykey.SecondaryKey => keys.SecondaryKey,
            _ => keys.PrimaryKey,
        };

        private string GetConnectionStringValue(Policykey key, IAuthorizationKeys keys) => key switch
        {
            Policykey.SecondaryKey => keys.SecondaryConnectionString,
            _ => keys.PrimaryConnectionString,
        };
    }
}