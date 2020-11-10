// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Integrations.CryptographicImplementations;
using AuthJanitor.Providers.Azure;
using AuthJanitor.Providers.Azure.Workflows;
using AuthJanitor.Providers.Capabilities;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core.CollectionActions;
using Microsoft.Azure.Management.ServiceBus.Fluent;
using Microsoft.Azure.Management.ServiceBus.Fluent.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.ServiceBus
{
    [Provider(Name = "Service Bus Key",
              Description = "Regenerates an Azure Service Bus Key",
              SvgImage = ProviderImages.SERVICE_BUS_SVG)]
    public class ServiceBusRekeyableObjectProvider : TwoKeyAzureRekeyableObjectProvider<ServiceBusKeyConfiguration, IServiceBusNamespace, IAuthorizationKeys, ServiceBusKeyConfiguration.ServiceBusKeyTypes, Policykey>,
        ICanEnumerateResourceCandidates
    {
        public ServiceBusRekeyableObjectProvider(ILogger<ServiceBusRekeyableObjectProvider> logger) : base(logger) { }

        protected override string Service => "Service Bus";

        protected override Policykey Translate(ServiceBusKeyConfiguration.ServiceBusKeyTypes keyType) => keyType switch
        {
            ServiceBusKeyConfiguration.ServiceBusKeyTypes.Primary => Policykey.PrimaryKey,
            ServiceBusKeyConfiguration.ServiceBusKeyTypes.Secondary => Policykey.SecondaryKey,
            _ => throw new NotImplementedException(),
        };

        protected override async Task<IAuthorizationKeys> RetrieveCurrentKeyring(IServiceBusNamespace resource, Policykey keyType) =>
            await (await resource.AuthorizationRules.GetByNameAsync(Configuration.AuthorizationRuleName)).GetKeysAsync();

        protected override async Task<IAuthorizationKeys> RotateKeyringValue(IServiceBusNamespace resource, Policykey keyType) =>
            await (await resource.AuthorizationRules.GetByNameAsync(Configuration.AuthorizationRuleName)).RegenerateKeyAsync(keyType);

        protected override RegeneratedSecret CreateSecretFromKeyring(IAuthorizationKeys keyring, ServiceBusKeyConfiguration.ServiceBusKeyTypes keyType) =>
            new RegeneratedSecret()
            {
                NewSecretValue = (keyType switch
                {
                    ServiceBusKeyConfiguration.ServiceBusKeyTypes.Primary => keyring.PrimaryKey,
                    ServiceBusKeyConfiguration.ServiceBusKeyTypes.Secondary => keyring.SecondaryKey,
                    _ => throw new NotImplementedException(),
                }).GetSecureString(),
                NewConnectionString = (keyType switch
                {
                    ServiceBusKeyConfiguration.ServiceBusKeyTypes.Primary => keyring.PrimaryConnectionString,
                    ServiceBusKeyConfiguration.ServiceBusKeyTypes.Secondary => keyring.SecondaryConnectionString,
                    _ => throw new NotImplementedException(),
                }).GetSecureString(),
            };

        protected override ISupportsGettingByResourceGroup<IServiceBusNamespace> GetResourceCollection(IAzure azure) => azure.ServiceBusNamespaces;

        public async Task<List<AuthJanitorProviderConfiguration>> EnumerateResourceCandidates(AuthJanitorProviderConfiguration baseConfig)
        {
            var azureConfig = baseConfig as AzureAuthJanitorProviderConfiguration;

            IPagedCollection<IServiceBusNamespace> items;
            if (!string.IsNullOrEmpty(azureConfig.ResourceGroup))
                items = await (await GetAzureAsync()).ServiceBusNamespaces.ListByResourceGroupAsync(azureConfig.ResourceGroup);
            else
                items = await (await GetAzureAsync()).ServiceBusNamespaces.ListAsync();

            return (await Task.WhenAll(items.Select(async i =>
            {
                var rules = await i.AuthorizationRules.ListAsync();
                return rules.Select(rule =>
                    new ServiceBusKeyConfiguration()
                    {
                        ResourceGroup = i.ResourceGroupName,
                        ResourceName = i.Name,
                        AuthorizationRuleName = rule.Name,
                        KeyType = ServiceBusKeyConfiguration.ServiceBusKeyTypes.Primary
                    } as AuthJanitorProviderConfiguration);
            }))).SelectMany(f => f).ToList();
        }
    }
}