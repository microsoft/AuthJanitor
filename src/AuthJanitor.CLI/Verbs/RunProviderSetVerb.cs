// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers;
using CommandLine;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AuthJanitor.CLI.Verbs
{
    [Verb("run", HelpText = "Execute provider configurations in the proper order with a given token")]
    public class RunProviderSetVerb : BaseVerb
    {
        [Option("file", HelpText = "Path to file containing provider configurations")]
        public string ProviderConfigurationFile { get; set; }

        public async Task Run(
            ILogger logger, 
            AuthJanitorService authJanitorService)
        {
            var loadedFile = File.ReadAllText(ProviderConfigurationFile);
            var parameters = JsonConvert.DeserializeObject<IEnumerable<ProviderExecutionParameters>>(loadedFile);

            if (parameters == null)
            {
                logger.LogError("Could not load configuration file");
                return;
            }

            var validPeriod = TimeSpan.FromDays(1);

            if (parameters.Any(p => p.AccessToken == null))
            {
                logger.LogInformation("Getting token");
                await EmbedToken();
                var token = JsonConvert.DeserializeObject<AccessTokenCredential>(AccessTokenString);
                foreach (var p in parameters.Where(p => p.AccessToken == null))
                    p.AccessToken = token;
            }

            var currentLog = string.Empty;
            Console.WriteLine("Running ");
            await authJanitorService.ExecuteAsync(
                validPeriod,
                async (c) => {
                    if (c.HasBeenExecuted)
                    {
                        Console.WriteLine();
                        foreach (var item in c.Actions)
                        {
                            Console.WriteLine($"{item.Start} => {item.End}");
                            Console.WriteLine(item.Log);
                            if (!item.HasSucceeded)
                                WriteError($"Action failed to execute: {item.Exception}");
                        }
                        Console.WriteLine();
                        Console.WriteLine("Orchestration Log:");
                        Console.WriteLine(c.OrchestrationLog);
                        Console.WriteLine();
                    }
                    else
                        Console.Write(".");
                    await Task.Yield();
                },
                parameters.ToArray());
        }
    }
}
