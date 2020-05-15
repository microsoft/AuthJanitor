// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.ComponentModel;

namespace AuthJanitor.Providers.EventHub
{
    public class EventHubKeyConfiguration : AuthJanitorProviderConfiguration
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
        public EventHubKeyTypes KeyType { get; set; }

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

        /// <summary>
        /// Skip the process of scrambling the other (non-active) key
        /// </summary>
        [DisplayName("Skip Scrambling Other Key?")]
        [Description("If checked, the opposite key (e.g. primary/secondary) will NOT be scrambled at the end of the rekeying")]
        public bool SkipScramblingOtherKey { get; set; }
    }
}
