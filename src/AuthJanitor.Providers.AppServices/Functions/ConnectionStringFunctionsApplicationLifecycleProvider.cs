// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Integrations.CryptographicImplementations;
using AuthJanitor.Providers.Azure;
using AuthJanitor.Providers.Azure.Workflows;
using AuthJanitor.Providers.Capabilities;
using Microsoft.Azure.Management.AppService.Fluent;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core.CollectionActions;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.AppServices.Functions
{
    [Provider(Name = "Functions App - Connection String",
              Description = "Manages the lifecycle of an Azure Functions app which reads from a Connection String",
              SvgImage = ProviderImages.FUNCTIONS_SVG)]
    public class ConnectionStringFunctionsApplicationLifecycleProvider : SlottableAzureApplicationLifecycleProvider<ConnectionStringConfiguration, IFunctionApp>,
        ICanEnumerateResourceCandidates
    {
        public ConnectionStringFunctionsApplicationLifecycleProvider(ProviderWorkflowActionLogger<AppSettingsFunctionsApplicationLifecycleProvider> logger) : base(logger) { }

        protected override async Task ApplyUpdate(IFunctionApp resource, string slotName, List<RegeneratedSecret> secrets)
        {
            var updateBase = (await resource.DeploymentSlots.GetByNameAsync(slotName)).Update();
            foreach (RegeneratedSecret secret in secrets)
            {
                var connectionStringName = string.IsNullOrEmpty(secret.UserHint) ? Configuration.ConnectionStringName : $"{Configuration.ConnectionStringName}-{secret.UserHint}";
                Logger.LogInformation("Updating Connection String '{ConnectionStringName}' in slot '{SlotName}'", connectionStringName, Configuration.TemporarySlot);
                updateBase = (Microsoft.Azure.Management.AppService.Fluent.FunctionDeploymentSlot.Update.IUpdate)
                              updateBase.WithoutConnectionString(connectionStringName);
                updateBase = (Microsoft.Azure.Management.AppService.Fluent.FunctionDeploymentSlot.Update.IUpdate)
                              updateBase.WithConnectionString(connectionStringName, secret.NewConnectionStringOrKey.GetNormalString(), Configuration.ConnectionStringType);
            }
            await updateBase.ApplyAsync();
        }

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

        public override string GetDescription() =>
            $"Populates a Connection String for '{Configuration.ConnectionStringType}' called " +
            $"'{Configuration.ConnectionStringName}' in an Azure " +
            $"Functions application called {Configuration.ResourceName} (Resource Group " +
            $"'{Configuration.ResourceGroup}'). During the rekeying, the Functions App will " +
            $"be moved from slot '{Configuration.SourceSlot}' to slot '{Configuration.TemporarySlot}' " +
            $"temporarily, and then back.";

        public async Task<List<AuthJanitorProviderConfiguration>> EnumerateResourceCandidates(AuthJanitorProviderConfiguration baseConfig)
        {
            var azureConfig = baseConfig as AzureAuthJanitorProviderConfiguration;

            IPagedCollection<IFunctionApp> items;
            if (!string.IsNullOrEmpty(azureConfig.ResourceGroup))
                items = await(await GetAzureAsync()).AppServices.FunctionApps.ListByResourceGroupAsync(azureConfig.ResourceGroup);
            else
                items = await(await GetAzureAsync()).AppServices.FunctionApps.ListAsync();

            return (await Task.WhenAll(items.Select(async i =>
            {
                return (await i.GetConnectionStringsAsync()).Select(c =>
                new ConnectionStringConfiguration()
                {
                    ResourceName = i.Name,
                    ResourceGroup = i.ResourceGroupName,
                    ConnectionStringName = c.Key,
                    ConnectionStringType = c.Value.Type
                } as AuthJanitorProviderConfiguration);
            }))).SelectMany(f => f).ToList();
        }
    }
}
