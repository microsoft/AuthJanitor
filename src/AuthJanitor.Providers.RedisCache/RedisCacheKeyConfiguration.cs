// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers.Azure.Workflows;
using System.ComponentModel;

namespace AuthJanitor.Providers.Redis
{
    public class RedisCacheKeyConfiguration : TwoKeyAzureAuthJanitorProviderConfiguration<RedisCacheKeyConfiguration.RedisKeyTypes>
    {
        public enum RedisKeyTypes
        {
            [PairedKey("key")]
            [Description("Primary")]
            Primary,
            [PairedKey("key")]
            [Description("Secondary")]
            Secondary
        }

        /// <summary>
        /// Type of Redis Key
        /// </summary>
        [DisplayName("Key Type")]
        [Description("Type of Redis Key to manage")]
        public override RedisKeyTypes KeyType { get; set; }
    }
}
