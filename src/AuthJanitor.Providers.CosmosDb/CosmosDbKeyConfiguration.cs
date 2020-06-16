// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers.Azure.Workflows;
using System.ComponentModel;

namespace AuthJanitor.Providers.CosmosDb
{
    public class CosmosDbKeyConfiguration : TwoKeyAzureAuthJanitorProviderConfiguration<CosmosDbKeyConfiguration.CosmosDbKeyKinds>
    {
        public enum CosmosDbKeyKinds
        {
            [PairedKey("rw")]
            [Description("Primary")]
            Primary,
            [PairedKey("rw")]
            [Description("Secondary")]
            Secondary,
            [PairedKey("ro")]
            [Description("Primary (Read-Only)")]
            PrimaryReadOnly,
            [PairedKey("ro")]
            [Description("Secondary (Read-Only)")]
            SecondaryReadOnly
        }

        /// <summary>
        /// Kind (type) of CosmosDb Key
        /// </summary>
        [DisplayName("Key Kind")]
        [Description("Kind of CosmosDB Key to manage")]
        public override CosmosDbKeyKinds KeyType { get; set; }
    }
}
