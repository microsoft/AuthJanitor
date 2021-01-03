// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.CryptographicImplementations;
using AuthJanitor.Providers;

namespace AuthJanitor.Integrations.CryptographicImplementations.AzureKeyVault
{
    public class AzureKeyVaultCryptographicImplementationConfiguration
    {
        public ICryptographicImplementation FallbackCryptography { get; set; }

        public string VaultUri { get; set; }
        public string KeyIdUri { get; set; }

        public TokenSources VaultTokenSource { get; set; }
    }
}
