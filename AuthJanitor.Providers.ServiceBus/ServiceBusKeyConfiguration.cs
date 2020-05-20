// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers.Azure.Workflows;
using System.ComponentModel;

namespace AuthJanitor.Providers.ServiceBus
{
    public class ServiceBusKeyConfiguration : TwoKeyAzureAuthJanitorProviderConfiguration<ServiceBusKeyConfiguration.ServiceBusKeyTypes>
    {
        /// <summary>
        /// Duplication of Service Bus "Policykey" enumeration to avoid passing through a dependency
        /// </summary>
        public enum ServiceBusKeyTypes
        {
            [PairedKey("key")]
            Primary,
            [PairedKey("key")]
            Secondary
        }

        /// <summary>
        /// Kind (type) of Service Bus Key
        /// </summary>
        [DisplayName("Service Bus Key Type")]
        [Description("Type of Service Bus Key to manage")]
        public override ServiceBusKeyTypes KeyType { get; set; }

        /// <summary>
        /// Service Bus Authorization Rule name
        /// </summary>
        [DisplayName("Authorization Rule")]
        [Description("Authorization Rule to manage")]
        public string AuthorizationRuleName { get; set; }
    }
}
