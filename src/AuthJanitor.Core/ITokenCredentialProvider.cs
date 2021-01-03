// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers;
using System.Threading.Tasks;

namespace AuthJanitor
{
    public interface ITokenCredentialProvider
    {
        public Task<AccessTokenCredential> GetToken(
            TokenSources source,
            string parameters);
    }
}
