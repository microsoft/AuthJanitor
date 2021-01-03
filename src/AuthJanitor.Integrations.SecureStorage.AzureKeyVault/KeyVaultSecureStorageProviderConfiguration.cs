// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers;
using System.ComponentModel;

namespace AuthJanitor.Integrations.SecureStorage.AzureKeyVault
{
    public class KeyVaultSecureStorageProviderConfiguration
    {
        [Description("Key Vault name")]
        public string VaultName { get; set; }

        [Description("Object prefix")]
        public string Prefix { get; set; } = "AJPersist-";

        public TokenSources TokenSource { get; set; }
    }
}
