// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.UI.Shared.ViewModels;
using System.Collections.Generic;

namespace AuthJanitor.ViewModels
{
    public class ProviderResourceSuggestionViewModel : IAuthJanitorViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string ProviderType { get; set; } = string.Empty;
        public string ProviderConfigurationSerialized { get; set; } = string.Empty;
        public ProviderConfigurationViewModel ProviderConfiguration { get; set; }
        public IEnumerable<ProviderResourceSuggestionViewModel> ResourcesAddressingThis { get; set; }
    }
}
