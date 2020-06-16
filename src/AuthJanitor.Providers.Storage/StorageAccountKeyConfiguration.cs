// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers.Azure.Workflows;
using System.ComponentModel;

namespace AuthJanitor.Providers.Storage
{
    public class StorageAccountKeyConfiguration : TwoKeyAzureAuthJanitorProviderConfiguration<StorageAccountKeyConfiguration.StorageKeyTypes>
    {
        public enum StorageKeyTypes
        {
            [PairedKey("key")]
            [Description("Key1")]
            Key1,
            [PairedKey("key")]
            [Description("Key2")]
            Key2,
            [PairedKey("kerb")]
            [Description("Kerb1")]
            Kerb1,
            [PairedKey("kerb")]
            [Description("Kerb2")]
            Kerb2
        }

        /// <summary>
        /// Kind (type) of Storage Key
        /// </summary>
        [DisplayName("Storage Key")]
        [Description("Type of Storage Key to manage")]
        public override StorageKeyTypes KeyType { get; set; }
    }
}
