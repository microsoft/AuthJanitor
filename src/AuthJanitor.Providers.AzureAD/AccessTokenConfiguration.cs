// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.ComponentModel;

namespace AuthJanitor.Providers.AzureAD
{
    public class AccessTokenConfiguration : AuthJanitorProviderConfiguration
    {
        /// <summary>
        /// Scopes/Resources to request for the Access Token
        /// </summary>
        [DisplayName("Token Scopes")]
        [Description("Access Token Scopes, comma-delimited")]
        public string[] Scopes { get; set; }

        /// <summary>
        /// Automatically refresh the Access Token
        /// </summary>
        [DisplayName("Auto-Refresh?")]
        [Description("Allow AuthJanitor to automatically refresh the Access Token when it expires?")]
        public bool AutomaticallyRefresh { get; set; }
    }
}
