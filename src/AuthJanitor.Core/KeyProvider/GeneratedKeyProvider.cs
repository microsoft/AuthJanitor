// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace AuthJanitor.KeyProvider
{
    public class GeneratedKeyProvider : IKeyProvider
    {
        public Task<byte[]> GetPrivateKey(string keyName)
        {
            return new byte[0];
        }

        public Task<byte[]> GetPublicKey(string keyName)
        {
            return new byte[0];
        }
    }
}
