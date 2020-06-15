// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.ComponentModel;

namespace AuthJanitor.Providers.KeyVault
{
    public class KeyVaultKeyConfiguration : AuthJanitorProviderConfiguration
    {
        /// <summary>
        /// Key Vault name (xxxxx.vault.azure.net)
        /// </summary>
        [DisplayName("Key Vault Name")]
        [Description("Name of Key Vault containing key to manage")]
        public string VaultName { get; set; }

        /// <summary>
        /// Name of Key or Secret being operated upon
        /// </summary>
        [DisplayName("Key Name")]
        [Description("Key Name to manage")]
        public string KeyName { get; set; }
    }
}
