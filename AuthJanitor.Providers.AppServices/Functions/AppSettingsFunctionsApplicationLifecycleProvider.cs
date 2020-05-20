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
    /// <summary>
    /// Defines a Functions application which receives key information through an AppConfig setting
    /// </summary>
    [Provider(Name = "Functions App - AppSettings",
              IconClass = "fa fa-bolt",
              Description = "Manages the lifecycle of an Azure Functions app which reads a Managed Secret from its Application Settings",
              Features = ProviderFeatureFlags.CanRotateWithoutDowntime |
                         ProviderFeatureFlags.IsTestable)]
    [ProviderImage(ProviderImages.FUNCTIONS_SVG)]
    public class AppSettingsFunctionsApplicationLifecycleProvider : SlottableAzureApplicationLifecycleProvider<AppSettingConfiguration, IFunctionApp>
    {
        private readonly ILogger _logger;

        public AppSettingsFunctionsApplicationLifecycleProvider(ILogger<AppSettingsFunctionsApplicationLifecycleProvider> logger) : base(logger)
        {
            _logger = logger;
        }
        
        protected override async Task ApplyUpdate(IFunctionApp resource, string slotName, List<RegeneratedSecret> secrets)
        {
            var updateBase = (await resource.DeploymentSlots.GetByNameAsync(slotName)).Update();
            foreach (RegeneratedSecret secret in secrets)
            {
                var appSettingName = string.IsNullOrEmpty(secret.UserHint) ? Configuration.SettingName : $"{Configuration.SettingName}-{secret.UserHint}";
                _logger.LogInformation("Updating AppSetting '{AppSettingName}' in slot '{SlotName}' (as {AppSettingType})", appSettingName, slotName,
                    Configuration.CommitAsConnectionString ? "connection string" : "secret");

                updateBase = (Microsoft.Azure.Management.AppService.Fluent.FunctionDeploymentSlot.Update.IUpdate)
                    updateBase.WithAppSetting(appSettingName,
                    Configuration.CommitAsConnectionString ? secret.NewConnectionStringOrKey : secret.NewSecretValue);
            }
            await updateBase.ApplyAsync();
        }

        public override string GetDescription() =>
            $"Populates an App Setting called '{Configuration.SettingName}' in an Azure " +
            $"Functions application called {Configuration.ResourceName} (Resource Group " +
            $"'{Configuration.ResourceGroup}'). During the rekeying, the Functions App will " +
            $"be moved from slot '{Configuration.SourceSlot}' to slot '{Configuration.TemporarySlot}' " +
            $"temporarily, and then to slot '{Configuration.DestinationSlot}'.";

        protected override Task SwapSlotAsync(IFunctionApp resource, string slotName) => resource.SwapAsync(slotName);

        protected override ISupportsGettingByResourceGroup<IFunctionApp> GetResourceCollection(IAzure azure) => azure.AppServices.FunctionApps;

        protected override async Task TestSlotAsync(IFunctionApp resource, string slotName)
        {
            var slot = await resource.DeploymentSlots.GetByNameAsync(slotName);
            if (slot == null) throw new System.Exception($"Slot {slotName} not found");
        }
    }
}