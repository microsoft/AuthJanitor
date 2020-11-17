// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Integrations.CryptographicImplementations;
using AuthJanitor.Providers.Azure;
using AuthJanitor.Providers.Azure.Workflows;
using AuthJanitor.Providers.Capabilities;
using Microsoft.Azure.Management.Eventhub.Fluent;
using Microsoft.Azure.Management.EventHub.Fluent.Models;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core.CollectionActions;
using Microsoft.Azure.Management.Sql.Fluent;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.EventHub
{
    [Provider(Name = "Event Hub Key",
              Description = "Regenerates an Azure Event Hub Key",
              SvgImage = ProviderImages.EVENT_HUB_SVG)]
    public class EventHubRekeyableObjectProvider : TwoKeyAzureRekeyableObjectProvider<EventHubKeyConfiguration, IEventHubNamespace, IEventHubAuthorizationKey, EventHubKeyConfiguration.EventHubKeyTypes, KeyType>,
        ICanEnumerateResourceCandidates
    {
        public EventHubRekeyableObjectProvider(ILogger<EventHubRekeyableObjectProvider> logger) : base(logger) { }

        protected override string Service => "Event Hub";

        public async Task<List<ProviderResourceSuggestion>> EnumerateResourceCandidates(AuthJanitorProviderConfiguration baseConfig)
        {
            var azureConfig = baseConfig as AzureAuthJanitorProviderConfiguration;

            IPagedCollection<IEventHubNamespace> items;
            if (!string.IsNullOrEmpty(azureConfig.ResourceGroup))
                items = await (await GetAzureAsync()).EventHubNamespaces.ListByResourceGroupAsync(azureConfig.ResourceGroup);
            else
                items = await (await GetAzureAsync()).EventHubNamespaces.ListAsync();

            return (await Task.WhenAll(items.Select(async i =>
            {
                var eventHubs = await (await GetAzureAsync()).EventHubs.ListByNamespaceAsync(i.ResourceGroupName, i.Name);
                return (await Task.WhenAll(eventHubs.Select(async eh =>
                {
                    var rules = await eh.ListAuthorizationRulesAsync();
                    return rules.Select(rule =>
                    new ProviderResourceSuggestion()
                    {
                        Configuration = new EventHubKeyConfiguration()
                        {
                            ResourceGroup = i.ResourceGroupName,
                            ResourceName = eh.Name,
                            NamespaceName = eh.NamespaceName,
                            AuthorizationRuleName = rule.Name
                        },
                        Name = $"Event Hub Key - {i.ResourceGroupName} - {eh.Name} - {eh.NamespaceName} ({rule.Name})",
                        ProviderType = this.GetType().AssemblyQualifiedName,
                        AddressableNames = new[] { i.ServiceBusEndpoint }
                    });
                }))).SelectMany(f => f);
            }))).SelectMany(f => f).ToList();
        }

        protected override RegeneratedSecret CreateSecretFromKeyring(IEventHubAuthorizationKey keyring, EventHubKeyConfiguration.EventHubKeyTypes keyType) =>
            new RegeneratedSecret()
            {
                NewSecretValue = (keyType switch
                {
                    EventHubKeyConfiguration.EventHubKeyTypes.Primary => keyring.PrimaryKey,
                    EventHubKeyConfiguration.EventHubKeyTypes.Secondary => keyring.SecondaryKey,
                    _ => throw new NotImplementedException(),
                }).GetSecureString(),
                NewConnectionString = (keyType switch
                {
                    EventHubKeyConfiguration.EventHubKeyTypes.Primary => keyring.PrimaryConnectionString,
                    EventHubKeyConfiguration.EventHubKeyTypes.Secondary => keyring.SecondaryConnectionString,
                    _ => throw new NotImplementedException(),
                }).GetSecureString()
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