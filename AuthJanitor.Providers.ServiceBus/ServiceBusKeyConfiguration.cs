// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.ComponentModel;

namespace AuthJanitor.Providers.ServiceBus
{
    public class ServiceBusKeyConfiguration : AuthJanitorProviderConfiguration
    {
        /// <summary>
        /// Duplication of Service Bus "Policykey" enumeration to avoid passing through a dependency
        /// </summary>
        public enum ServiceBusKeyTypes
        {
            Primary,
            Secondary
        }

        /// <summary>
        /// Kind (type) of Service Bus Key
        /// </summary>
        [DisplayName("Service Bus Key Type")]
        [Description("Type of Service Bus Key to manage")]
        public ServiceBusKeyTypes KeyType { get; set; }

        /// <summary>
        /// Service Bus Authorization Rule name
        /// </summary>
        [DisplayName("Authorization Rule")]
        [Description("Authorization Rule to manage")]
        public string AuthorizationRuleName { get; set; }

        /// <summary>
        /// Skip the process of scrambling the other (non-active) key
        /// </summary>
        [DisplayName("Skip Scrambling Other Key?")]
        [Description("If checked, the opposite key (e.g. primary/secondary) will NOT be scrambled at the end of the rekeying")]
        public bool SkipScramblingOtherKey { get; set; }
    }
}
