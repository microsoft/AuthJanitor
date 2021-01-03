// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Azure.Core;
using Azure.Identity;
using CommandLine;
using System;
using System.Threading.Tasks;

namespace AuthJanitor.CLI.Verbs
{
    public abstract class BaseVerb
    {
        [Option('u', "user")]
        public bool UseUserIdentity { get; set; }

        [Option('s', "system")]
        public bool UseSystemIdentity { get; set; }

        [Option("token", HelpText = "Existing access token")]
        public string AccessTokenString { get; set; }

        protected async Task EmbedToken()
        {
            var c = new DefaultAzureCredential();
            var token = await c.GetTokenAsync(new TokenRequestContext(
                new[] { "https://management.azure.com/.default" }));
            AccessTokenString = token.Token;
        }

        protected void WriteError(string error)
        {
            Console.WriteLine("[ERROR] " + error);
        }
    }
}
