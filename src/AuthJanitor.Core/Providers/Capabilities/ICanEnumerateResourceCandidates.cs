// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.Capabilities
{
    public interface ICanEnumerateResourceCandidates : IAuthJanitorCapability
    {
        Task<List<AuthJanitorProviderConfiguration>> EnumerateResourceCandidates(AuthJanitorProviderConfiguration baseConfig);
    }
}