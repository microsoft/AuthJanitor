// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.ComponentModel;

namespace AuthJanitor.Providers.KeyVault
{
    public class KeyVaultSecretLifecycleConfiguration : AuthJanitorProviderConfiguration
    {
        /// <summary>
        /// Key Vault name (xxxxx.vault.azure.net)
        /// </summary>
        [DisplayName("Key Vault Name")]
        [Description("Name of Key Vault containing key to manage")]
        public string VaultName { get; set; }

        /// <summary>
        /// Name Secret being operated upon
        /// </summary>
        [DisplayName("Secret Name")]
        [Description("Secret Name to manage")]
        public string SecretName { get; set; }

        /// <summary>
        /// Commit the ConnectionString instead of the Key
        /// </summary>
        [DisplayName("Commit Connection String")]
        [Description("Commit a Connection String instead of a Key to this AppSetting, when available")]
        public bool CommitAsConnectionString { get; set; }
    }
}
