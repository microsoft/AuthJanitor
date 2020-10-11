// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers.Capabilities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthJanitor.Providers
{
    public interface IProviderFactory
    {
        Task<List<ProviderResourceCandidate>> GetProviders();
        Task GetProviders(List<ProviderResourceCandidate> providers);
    }
}
