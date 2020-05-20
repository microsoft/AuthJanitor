// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
using System.ComponentModel;

namespace AuthJanitor.Providers.Azure.Workflows
{
    /// <summary>
    /// Defines confguration for an Azure service supporting two-key rotation.
    /// </summary>
    /// <typeparam name="TKeyEnum">Enumeration defining possible key types</typeparam>
    public abstract class TwoKeyAzureAuthJanitorProviderConfiguration<TKeyEnum> : AzureAuthJanitorProviderConfiguration
        where TKeyEnum : Enum
    {
        public abstract TKeyEnum KeyType { get; set; }

        /// <summary>
        /// Skip the process of scrambling the other (non-active) key
        /// </summary>
        [DisplayName("Skip Scrambling Other Key?")]
        [Description("If checked, the opposite key (e.g. primary/secondary) will NOT be scrambled at the end of the rekeying")]
        public bool SkipScramblingOtherKey { get; set; }
    }
}
