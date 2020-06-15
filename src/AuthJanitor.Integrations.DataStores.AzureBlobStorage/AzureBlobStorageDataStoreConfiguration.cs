// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.ComponentModel;

namespace AuthJanitor.Integrations.DataStores.AzureBlobStorage
{
    public class AzureBlobStorageDataStoreConfiguration
    {
        [Description("Connection string for Azure Blob Storage")]
        public string ConnectionString { get; set; }

        [Description("Blob storage container name")]
        public string Container { get; set; }
    }
}
