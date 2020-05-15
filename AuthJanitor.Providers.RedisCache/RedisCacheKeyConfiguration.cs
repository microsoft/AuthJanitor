// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.ComponentModel;

namespace AuthJanitor.Providers.Redis
{
    public class RedisCacheKeyConfiguration : AuthJanitorProviderConfiguration
    {
        public enum RedisKeyTypes
        {
            [Description("Primary")]
            Primary,
            [Description("Secondary")]
            Secondary
        }

        /// <summary>
        /// Kind (type) of Redis Key
        /// </summary>
        [DisplayName("Key Kind")]
        [Description("Kind of Redis Key to manage")]
        public RedisKeyTypes KeyType { get; set; }

        /// <summary>
        /// Skip the process of scrambling the other (non-active) key
        /// </summary>
        [DisplayName("Skip Scrambling Other Key?")]
        [Description("If checked, the opposite key (e.g. primary/secondary) will NOT be scrambled at the end of the rekeying")]
        public bool SkipScramblingOtherKey { get; set; }
    }
}
