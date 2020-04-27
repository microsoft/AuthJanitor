// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.ComponentModel;

namespace AuthJanitor.Providers.CosmosDb
{
    public class CosmosDbKeyConfiguration : AuthJanitorProviderConfiguration
    {
        public enum CosmosDbKeyKinds
        {
            [Description("Primary")]
            Primary,
            [Description("Secondary")]
            Secondary,
            [Description("Primary (Read-Only)")]
            PrimaryReadOnly,
            [Description("Secondary (Read-Only)")]
            SecondaryReadOnly
        }

        /// <summary>
        /// Kind (type) of CosmosDb Key
        /// </summary>
        [DisplayName("Key Kind")]
        [Description("Kind of CosmosDB Key to manage")]
        public CosmosDbKeyKinds KeyKind { get; set; }

        /// <summary>
        /// Skip the process of scrambling the other (non-active) key
        /// </summary>
        [DisplayName("Skip Scrambling Other Key?")]
        [Description("If checked, the opposite key (e.g. primary/secondary) will NOT be scrambled at the end of the rekeying")]
        public bool SkipScramblingOtherKey { get; set; }
    }
}
