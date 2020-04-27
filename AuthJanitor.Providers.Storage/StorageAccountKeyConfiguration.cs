// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.ComponentModel;

namespace AuthJanitor.Providers.Storage
{
    public class StorageAccountKeyConfiguration : AuthJanitorProviderConfiguration
    {
        public enum StorageKeyTypes
        {
            [Description("Key1")]
            Key1,
            [Description("Key2")]
            Key2,
            [Description("Kerb1")]
            Kerb1,
            [Description("Kerb2")]
            Kerb2
        }

        /// <summary>
        /// Kind (type) of Storage Key
        /// </summary>
        [DisplayName("Storage Key")]
        [Description("Type of Storage Key to manage")]
        public StorageKeyTypes KeyType { get; set; }

        /// <summary>
        /// Skip the process of scrambling the other (non-active) key
        /// </summary>
        [DisplayName("Skip Scrambling Other Key?")]
        [Description("If checked, the opposite key (e.g. primary/secondary) will NOT be scrambled at the end of the rekeying")]
        public bool SkipScramblingOtherKey { get; set; }
    }
}
