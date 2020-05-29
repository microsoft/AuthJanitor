// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers.Azure;
using System.ComponentModel;

namespace AuthJanitor.Providers.AzureMaps
{
    public class AzureMapsConfiguration : AzureAuthJanitorProviderConfiguration
    {
        public enum AzureMapsKeyType
        {
            [Description("Primary")]
            Primary,
            [Description("Secondary")]
            Secondary
        }

        /// <summary>
        /// Azure Maps Key Type
        /// </summary>
        [DisplayName("Key Type")]
        public AzureMapsKeyType KeyType { get; set; }

        /// <summary>
        /// Skip the process of scrambling the other (non-active) key
        /// </summary>
        [DisplayName("Skip Scrambling Other Key?")]
        [Description("If checked, the opposite key (e.g. primary/secondary) will NOT be scrambled at the end of the rekeying")]
        public bool SkipScramblingOtherKey { get; set; }
    }
}
