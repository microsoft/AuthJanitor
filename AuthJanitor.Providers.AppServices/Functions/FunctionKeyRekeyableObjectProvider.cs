// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.AppServices.Functions
{
    [Provider(Name = "Functions App Key",
              IconClass = "fa fa-key",
              Description = "Regenerates a Function Key for an Azure Functions application")]
    [ProviderImage(ProviderImages.FUNCTIONS_SVG)]
    public class FunctionKeyRekeyableObjectProvider : RekeyableObjectProvider<FunctionKeyConfiguration>
    {
        public FunctionKeyRekeyableObjectProvider(ILogger logger, IServiceProvider serviceProvider) : base(logger, serviceProvider)
        {
        }

        public override async Task Test()
        {
            var functionsApp = await (await GetAzure()).AppServices.FunctionApps.GetByResourceGroupAsync(ResourceGroup, ResourceName);
            if (functionsApp == null)
                throw new Exception($"Cannot locate Functions application called '{ResourceName}' in group '{ResourceGroup}'");
            var keys = await functionsApp.ListFunctionKeysAsync(Configuration.FunctionName);
            if (keys == null)
                throw new Exception($"Cannot list Function Keys for Function '{Configuration.FunctionName}'");
        }

        public override async Task<RegeneratedSecret> Rekey(TimeSpan requestedValidPeriod)
        {
            Logger.LogInformation("Generating a new secret of length {0}", Configuration.KeyLength);
            RegeneratedSecret newKey = new RegeneratedSecret()
            {
                Expiry = DateTimeOffset.UtcNow + requestedValidPeriod,
                UserHint = Configuration.UserHint,
                NewSecretValue = HelperMethods.GenerateCryptographicallySecureString(Configuration.KeyLength)
            };

            var functionsApp = await (await GetAzure()).AppServices.FunctionApps.GetByResourceGroupAsync(ResourceGroup, ResourceName);
            if (functionsApp == null)
                throw new Exception($"Cannot locate Functions application called '{ResourceName}' in group '{ResourceGroup}'");

            Logger.LogInformation("Removing previous Function Key '{0}' from Function '{1}'", Configuration.FunctionKeyName, Configuration.FunctionName);
            await functionsApp.RemoveFunctionKeyAsync(Configuration.FunctionName, Configuration.FunctionKeyName);

            Logger.LogInformation("Adding new Function Key '{0}' from Function '{1}'", Configuration.FunctionKeyName, Configuration.FunctionName);
            await functionsApp.AddFunctionKeyAsync(Configuration.FunctionName, Configuration.FunctionKeyName, newKey.NewSecretValue);

            return newKey;
        }

        public override string GetDescription() =>
            $"Regenerates a Functions key for an Azure " +
            $"Functions application called {Configuration.ResourceName} (Resource Group " +
            $"'{Configuration.ResourceGroup}').";

        // TODO: Zero-downtime rotation here with similar slotting?
        //During the rekeying, the Functions App will " +
        //    $"be moved from slot '{Configuration.SourceSlot}' to slot '{Configuration.TemporarySlot}' " +
        //    $"temporarily, and then to slot '{Configuration.DestinationSlot}'.";
    }
}
