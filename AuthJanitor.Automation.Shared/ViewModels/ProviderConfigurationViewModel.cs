// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.Collections.Generic;

namespace AuthJanitor.Automation.Shared.ViewModels
{
    public class ProviderConfigurationViewModel : IAuthJanitorViewModel
    {
        public IEnumerable<ProviderConfigurationItemViewModel> ConfigurationItems { get; set; } = new List<ProviderConfigurationItemViewModel>();
    }
}
