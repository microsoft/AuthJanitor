// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.ComponentModel;

namespace AuthJanitor.Providers.AzureSearch
{
    public class AzureSearchAdminKeyConfiguration : AuthJanitorProviderConfiguration
    {
        public enum AzureSearchKeyKinds
        {
            [Description("Primary")]
            Primary,
            [Description("Secondary")]
            Secondary
        }

        /// <summary>
        /// Kind (type) of Azure Search Key
        /// </summary>
        [DisplayName("Key Kind")]
        [Description("Primary or secondary administrative key")]
        public AzureSearchKeyKinds KeyKind { get; set; }

        /// <summary>
        /// Skip the process of scrambling the other (non-active) key
        /// </summary>
        [DisplayName("Skip Scrambling Other Key?")]
        [Description("If checked, the opposite key (e.g. primary/secondary) will NOT be scrambled at the end of the rekeying")]
        public bool SkipScramblingOtherKey { get; set; }
    }
}