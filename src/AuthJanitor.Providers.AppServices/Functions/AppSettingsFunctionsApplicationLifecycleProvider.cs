// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Integrations.CryptographicImplementations;
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
              Description = "Manages the lifecycle of an Azure Functions app which reads a Managed Secret from its Application Settings",
              SvgImage = ProviderImages.FUNCTIONS_SVG)]
    public class AppSettingsFunctionsApplicationLifecycleProvider : SlottableAzureApplicationLifecycleProvider<AppSettingConfiguration, IFunctionApp>
    {
        public AppSettingsFunctionsApplicationLifecycleProvider(ProviderWorkflowActionLogger<AppSettingsFunctionsApplicationLifecycleProvider> logger) : base(logger) { }

        protected override async Task ApplyUpdate(IFunctionApp resource, string slotName, List<RegeneratedSecret> secrets)
        {
            var updateBase = (await resource.DeploymentSlots.GetByNameAsync(slotName)).Update();
            foreach (RegeneratedSecret secret in secrets)
            {
                var appSettingName = string.IsNullOrEmpty(secret.UserHint) ? Configuration.SettingName : $"{Configuration.SettingName}-{secret.UserHint}";
                Logger.LogInformation("Updating AppSetting '{AppSettingName}' in slot '{SlotName}' (as {AppSettingType})", appSettingName, slotName,
                    Configuration.CommitAsConnectionString ? "connection string" : "secret");

                updateBase = (Microsoft.Azure.Management.AppService.Fluent.FunctionDeploymentSlot.Update.IUpdate)
                    updateBase.WithAppSetting(appSettingName,
                    Configuration.CommitAsConnectionString ? secret.NewConnectionStringOrKey.GetNormalString() : secret.NewSecretValue.GetNormalString());
            }
            await updateBase.ApplyAsync();
        }

        public override string GetDescription() =>
            $"Populates an App Setting called '{Configuration.SettingName}' in an Azure " +
            $"Functions application called {Configuration.ResourceName} (Resource Group " +
            $"'{Configuration.ResourceGroup}'). During the rekeying, the Functions App will " +
            $"be moved from slot '{Configuration.SourceSlot}' to slot '{Configuration.TemporarySlot}' " +
            $"temporarily, and then back.";

        protected override async Task SwapSlotAsync(IFunctionApp resource, string sourceSlotName) =>
            await resource.SwapAsync(sourceSlotName);

        protected override async Task SwapSlotAsync(IFunctionApp resource, string sourceSlotName, string destinationSlotName) =>
            await (await resource.DeploymentSlots.GetByNameAsync(destinationSlotName))
                .SwapAsync(sourceSlotName);

        protected override ISupportsGettingByResourceGroup<IFunctionApp> GetResourceCollection(IAzure azure) => azure.AppServices.FunctionApps;

        protected override async Task TestSlotAsync(IFunctionApp resource, string slotName)
        {
            var slot = await resource.DeploymentSlots.GetByNameAsync(slotName);
            if (slot == null) throw new System.Exception($"Slot {slotName} not found");
        }
    }
}