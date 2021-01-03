// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.Collections.Generic;

namespace AuthJanitor.Providers
{
    public class ProviderResourceSuggestion
    {
        public string Name { get; set; }
        public string ProviderType { get; set; }

        public IEnumerable<string> AddressableNames { get; set; }
        public IEnumerable<string> ResourceValues { get; set; }
        
        public List<ProviderResourceSuggestion> ResourcesAddressingThis { get; set; }

        public AuthJanitorProviderConfiguration Configuration { get; set; }
        public string SerializedConfiguration { get; set; }
        public ProviderExecutionParameters Parameters =>
            new ProviderExecutionParameters()
            {
                ProviderType = ProviderType,
                ProviderConfiguration = SerializedConfiguration,
            };

        public ProviderResourceSuggestion()
        {
            AddressableNames = new List<string>();
            ResourceValues = new List<string>();
            ResourcesAddressingThis = new List<ProviderResourceSuggestion>();
        }
    }
}
