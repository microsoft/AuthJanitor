// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
namespace AuthJanitor.Providers
{
    public class ProviderResourceSuggestion
    {
        public string Name { get; set; }
        public string ProviderType { get; set; }

        public AuthJanitorProviderConfiguration Configuration { get; set; }
        public string SerializedConfiguration { get; set; }
    }
}
