// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers.Azure.Workflows;
using Microsoft.Azure.Management.AppService.Fluent;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core.CollectionActions;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.AppServices.Functions
{
    [Provider(Name = "Functions App - Connection String",
              IconClass = "fa fa-bolt",
              Description = "Manages the lifecycle of an Azure Functions app which reads from a Connection String",
              Features = ProviderFeatureFlags.CanRotateWithoutDowntime |
                         ProviderFeatureFlags.IsTestable)]
    [ProviderImage(ProviderImages.FUNCTIONS_SVG)]
    public class ConnectionStringFunctionsApplicationLifecycleProvider : SlottableAzureApplicationLifecycleProvider<ConnectionStringConfiguration, IFunctionApp>
    {
        private readonly ILogger _logger;

        public ConnectionStringFunctionsApplicationLifecycleProvider(ILogger<AppSettingsFunctionsApplicationLifecycleProvider> logger) : base(logger)
        {
            _logger = logger;
        }

        protected override async Task ApplyUpdate(IFunctionApp resource, string slotName, List<RegeneratedSecret> secrets)
        {
            var updateBase = (await resource.DeploymentSlots.GetByNameAsync(slotName)).Update();
            foreach (RegeneratedSecret secret in secrets)
            {
                var connectionStringName = string.IsNullOrEmpty(secret.UserHint) ? Configuration.ConnectionStringName : $"{Configuration.ConnectionStringName}-{secret.UserHint}";
                _logger.LogInformation("Updating Connection String '{ConnectionStringName}' in slot '{SlotName}'", connectionStringName, Configuration.TemporarySlot);
                updateBase = (Microsoft.Azure.Management.AppService.Fluent.FunctionDeploymentSlot.Update.IUpdate)
                              updateBase.WithoutConnectionString(connectionStringName);
                updateBase = (Microsoft.Azure.Management.AppService.Fluent.FunctionDeploymentSlot.Update.IUpdate)
                              updateBase.WithConnectionString(connectionStringName, secret.NewConnectionStringOrKey, Configuration.ConnectionStringType);
            }
            await updateBase.ApplyAsync();
        }

        protected override Task SwapSlotAsync(IFunctionApp resource, string slotName) => resource.SwapAsync(slotName);

        protected override ISupportsGettingByResourceGroup<IFunctionApp> GetResourceCollection(IAzure azure) => azure.AppServices.FunctionApps;

        protected override async Task TestSlotAsync(IFunctionApp resource, string slotName)
        {
            var slot = await resource.DeploymentSlots.GetByNameAsync(slotName);
            if (slot == null) throw new System.Exception($"Slot {slotName} not found");
        }

        public override string GetDescription() =>
            $"Populates a Connection String for '{Configuration.ConnectionStringType}' called " +
            $"'{Configuration.ConnectionStringName}' in an Azure " +
            $"Functions application called {Configuration.ResourceName} (Resource Group " +
            $"'{Configuration.ResourceGroup}'). During the rekeying, the Functions App will " +
            $"be moved from slot '{Configuration.SourceSlot}' to slot '{Configuration.TemporarySlot}' " +
            $"temporarily, and then to slot '{Configuration.DestinationSlot}'.";
    }
}
