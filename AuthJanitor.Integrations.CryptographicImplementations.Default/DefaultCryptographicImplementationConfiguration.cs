// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.ComponentModel;

namespace AuthJanitor.Integrations.CryptographicImplementations.Default
{
    public class DefaultCryptographicImplementationConfiguration
    {
        [Description("Encryption key to use (with salt) when encrypting/decrypting data")]
        public string MasterEncryptionKey { get; set; }
    }
}
