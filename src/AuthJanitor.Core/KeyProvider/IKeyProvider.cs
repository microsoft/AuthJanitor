// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.Threading.Tasks;

namespace AuthJanitor.KeyProvider
{
    public interface IKeyProvider
    {
        Task<byte[]> GetPublicKey(string keyName);
        Task<byte[]> GetPrivateKey(string keyName);
    }
}
