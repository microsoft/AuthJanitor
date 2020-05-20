// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers.Azure.Workflows;
using Microsoft.Azure.Management.Eventhub.Fluent;
using Microsoft.Azure.Management.EventHub.Fluent.Models;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core.CollectionActions;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.EventHub
{
    [Provider(Name = "Event Hub Key",
              IconClass = "fa fa-key",
              Description = "Regenerates an Azure Event Hub Key",
              Features = ProviderFeatureFlags.CanRotateWithoutDowntime |
                         ProviderFeatureFlags.IsTestable |
                         ProviderFeatureFlags.SupportsSecondaryKey)]
    [ProviderImage(ProviderImages.EVENT_HUB_SVG)]
    public class EventHubRekeyableObjectProvider : TwoKeyAzureRekeyableObjectProvider<EventHubKeyConfiguration, IEventHubNamespace, IEventHubAuthorizationKey, EventHubKeyConfiguration.EventHubKeyTypes, KeyType>
    {
        public EventHubRekeyableObjectProvider(ILogger<EventHubRekeyableObjectProvider> logger) : base(logger) { }

        protected override string Service => "Event Hub";
        
        protected override RegeneratedSecret CreateSecretFromKeyring(IEventHubAuthorizationKey keyring, EventHubKeyConfiguration.EventHubKeyTypes keyType) =>
            new RegeneratedSecret()
            {
                NewSecretValue = keyType switch
                {
                    EventHubKeyConfiguration.EventHubKeyTypes.Primary => keyring.PrimaryKey,
                    EventHubKeyConfiguration.EventHubKeyTypes.Secondary => keyring.SecondaryKey,
                    _ => throw new NotImplementedException(),
                },
                NewConnectionString = keyType switch
                {
                    EventHubKeyConfiguration.EventHubKeyTypes.Primary => keyring.PrimaryConnectionString,
                    EventHubKeyConfiguration.EventHubKeyTypes.Secondary => keyring.SecondaryConnectionString,
                    _ => throw new NotImplementedException(),
                }
            };

        protected override ISupportsGettingByResourceGroup<IEventHubNamespace> GetResourceCollection(IAzure azure) => azure.EventHubNamespaces;

        protected override async Task<IEventHubAuthorizationKey> RetrieveCurrentKeyring(IEventHubNamespace resource, KeyType keyType) =>
            await (await resource.ListAuthorizationRulesAsync()).First(r => r.Name == Configuration.AuthorizationRuleName).GetKeysAsync();

        protected override async Task<IEventHubAuthorizationKey> RotateKeyringValue(IEventHubNamespace resource, KeyType keyType) =>
            await (await resource.ListAuthorizationRulesAsync()).First(r => r.Name == Configuration.AuthorizationRuleName).RegenerateKeyAsync(keyType);

        protected override KeyType Translate(EventHubKeyConfiguration.EventHubKeyTypes keyType) => keyType switch
        {
            EventHubKeyConfiguration.EventHubKeyTypes.Primary => KeyType.PrimaryKey,
            EventHubKeyConfiguration.EventHubKeyTypes.Secondary => KeyType.SecondaryKey,
            _ => throw new NotImplementedException(),
        };
    }
}