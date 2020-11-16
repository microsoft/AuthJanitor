// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.Capabilities
{
    public interface ICanEnumerateResourceCandidates : IAuthJanitorCapability
    {
        Task<List<ProviderResourceSuggestion>> EnumerateResourceCandidates(AuthJanitorProviderConfiguration baseConfig);
    }
}