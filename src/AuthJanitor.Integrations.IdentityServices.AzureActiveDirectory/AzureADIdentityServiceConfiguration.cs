// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers;
using System.ComponentModel;

namespace AuthJanitor.Integrations.IdentityServices.AzureActiveDirectory
{
    public class AzureADIdentityServiceConfiguration
    {
        [Description("Azure AD application's client ID")]
        public string ClientId { get; set; }

        [Description("Azure AD application's client secret")]
        public string ClientSecret { get; set; }

        [Description("Azure AD tenant ID")]
        public string TenantId { get; set; }

        [Description("Token Source")]
        public TokenSources TokenSource { get; set; }
    }
}
