// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.ComponentModel;

namespace AuthJanitor.Providers.KeyVault
{
    public class KeyVaultSecretConfiguration : AuthJanitorProviderConfiguration
    {
        public const int DEFAULT_SECRET_LENGTH = 64;

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
        /// Length of secret to regenerate
        /// </summary>
        [DisplayName("Secret Length")]
        [Description("Length of secret to generate")]
        public int SecretLength { get; set; } = DEFAULT_SECRET_LENGTH;
    }
}
