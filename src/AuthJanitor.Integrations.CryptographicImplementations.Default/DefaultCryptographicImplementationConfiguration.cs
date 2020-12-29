// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.Collections.Generic;

namespace AuthJanitor.Integrations.CryptographicImplementations.Default
{
    public class DefaultCryptographicImplementationConfiguration
    { 
        public byte[] PublicKey { get; set; }
        public byte[] PrivateKey { get; set; }

        public Dictionary<string, byte[]> OtherPublicKeys { get; set; } = new
            Dictionary<string, byte[]>();

        public Dictionary<string, byte[]> OtherPrivateKeys { get; set; } = new
            Dictionary<string, byte[]>();
    }
}
