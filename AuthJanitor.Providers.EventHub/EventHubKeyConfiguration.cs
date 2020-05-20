// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers.Azure.Workflows;
using System.ComponentModel;

namespace AuthJanitor.Providers.EventHub
{
    public class EventHubKeyConfiguration : TwoKeyAzureAuthJanitorProviderConfiguration<EventHubKeyConfiguration.EventHubKeyTypes>
    {
        /// <summary>
        /// Duplication of Service Bus "Policykey" enumeration to avoid passing through a dependency
        /// </summary>
        public enum EventHubKeyTypes
        {
            Primary,
            Secondary
        }

        /// <summary>
        /// Kind (type) of Event Hub Key
        /// </summary>
        [DisplayName("Event Hub Key Type")]
        public override EventHubKeyTypes KeyType { get; set; }

        /// <summary>
        /// Event Hub Authorization Rule name
        /// </summary>
        [DisplayName("Event Hub Namespace")]
        public string NamespaceName { get; set; }

        /// <summary>
        /// Event Hub Authorization Rule name
        /// </summary>
        [DisplayName("Authorization Rule")]
        public string AuthorizationRuleName { get; set; }
    }
}
