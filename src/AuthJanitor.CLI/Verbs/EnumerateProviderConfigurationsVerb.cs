// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using CommandLine;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace AuthJanitor.CLI.Verbs
{
    [Verb("enumerate", HelpText = "Enumerate provider configurations with a given token")]
    public class EnumerateProviderConfigurationsVerb : BaseVerb
    {
        public async Task Run(
            ILogger logger,
            AuthJanitorService authJanitorService)
        {
            logger.LogInformation("Getting token");
            await EmbedToken();
            logger.LogInformation("Enumerating (this will take a minute) ...");
            var enumerated = await authJanitorService.EnumerateAsync(
                JsonConvert.DeserializeObject<AccessTokenCredential>(AccessTokenString));
            foreach (var obj in enumerated)
            {
                Console.WriteLine(obj.Name);
                Console.WriteLine(obj.ProviderType);
                Console.WriteLine();
                Console.WriteLine(obj.SerializedConfiguration);
                Console.WriteLine("\n");
            }
        }
    }
}
