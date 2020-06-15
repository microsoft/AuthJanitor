// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers.Azure.Workflows;
using System.ComponentModel;

namespace AuthJanitor.Providers.AzureSearch
{
    public class AzureSearchAdminKeyConfiguration : TwoKeyAzureAuthJanitorProviderConfiguration<AzureSearchAdminKeyConfiguration.AzureSearchKeyKinds>
    {
        public enum AzureSearchKeyKinds
        {
            [PairedKey("key")]
            [Description("Primary")]
            Primary,
            [PairedKey("key")]
            [Description("Secondary")]
            Secondary
        }

        /// <summary>
        /// Kind (type) of Azure Search Key
        /// </summary>
        [DisplayName("Key Kind")]
        [Description("Primary or secondary administrative key")]
        public override AzureSearchKeyKinds KeyType { get; set; }
    }
}