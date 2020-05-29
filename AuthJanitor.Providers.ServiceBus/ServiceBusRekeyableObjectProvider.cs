// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers.Azure.Workflows;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core.CollectionActions;
using Microsoft.Azure.Management.ServiceBus.Fluent;
using Microsoft.Azure.Management.ServiceBus.Fluent.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.ServiceBus
{
    [Provider(Name = "Service Bus Key",
              IconClass = "fa fa-key",
              Description = "Regenerates an Azure Service Bus Key",
              Features = ProviderFeatureFlags.CanRotateWithoutDowntime |
                         ProviderFeatureFlags.IsTestable |
                         ProviderFeatureFlags.SupportsSecondaryKey)]
    [ProviderImage(ProviderImages.SERVICE_BUS_SVG)]
    public class ServiceBusRekeyableObjectProvider : TwoKeyAzureRekeyableObjectProvider<ServiceBusKeyConfiguration, IServiceBusNamespace, IAuthorizationKeys, ServiceBusKeyConfiguration.ServiceBusKeyTypes, Policykey>
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
                NewSecretValue = keyType switch
                {
                    ServiceBusKeyConfiguration.ServiceBusKeyTypes.Primary => keyring.PrimaryKey,
                    ServiceBusKeyConfiguration.ServiceBusKeyTypes.Secondary => keyring.SecondaryKey,
                    _ => throw new NotImplementedException(),
                },
                NewConnectionString = keyType switch
                {
                    ServiceBusKeyConfiguration.ServiceBusKeyTypes.Primary => keyring.PrimaryConnectionString,
                    ServiceBusKeyConfiguration.ServiceBusKeyTypes.Secondary => keyring.SecondaryConnectionString,
                    _ => throw new NotImplementedException(),
                },
            };

        protected override ISupportsGettingByResourceGroup<IServiceBusNamespace> GetResourceCollection(IAzure azure) => azure.ServiceBusNamespaces;
    }
}