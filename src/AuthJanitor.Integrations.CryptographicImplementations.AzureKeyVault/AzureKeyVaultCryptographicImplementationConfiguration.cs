// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace AuthJanitor.Integrations.CryptographicImplementations.AzureKeyVault
{
    public class AzureKeyVaultCryptographicImplementationConfiguration
    {
        public ICryptographicImplementation FallbackCryptography { get; set; }

        public string VaultUri { get; set; }
        public string KeyIdUri { get; set; }
    }
}
