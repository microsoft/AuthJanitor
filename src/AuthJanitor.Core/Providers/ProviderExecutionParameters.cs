// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
namespace AuthJanitor.Providers
{
    public enum TokenSources
    {
        Unknown,
        Explicit,
        Persisted,
        ServicePrincipal,
        OBO
    }

    public class ProviderExecutionParameters
    {
        public string ProviderType { get; set; }
        public string ProviderConfiguration { get; set; }

        public TokenSources TokenSource { get; set; }
        public string TokenParameter { get; set; }
        public string AgentId { get; set; } = "admin-service";

        public AccessTokenCredential AccessToken { get; set; }
    }
}
