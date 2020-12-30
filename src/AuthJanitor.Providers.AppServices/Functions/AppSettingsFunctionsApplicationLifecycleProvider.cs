// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.CryptographicImplementations;
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
    /// <summary>
    /// Defines a Functions application which receives key information through an AppConfig setting
    /// </summary>
    [Provider(Name = "Functions App - AppSettings",
              Description = "Manages the lifecycle of an Azure Functions app which reads a Managed Secret from its Application Settings",
              SvgImage = ProviderImages.FUNCTIONS_SVG)]
    public class AppSettingsFunctionsApplicationLifecycleProvider : 
        SlottableAzureApplicationLifecycleProvider<AppSettingConfiguration, IFunctionApp>,
        ICanEnumerateResourceCandidates
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

        public async Task<List<ProviderResourceSuggestion>> EnumerateResourceCandidates(AuthJanitorProviderConfiguration baseConfig)
        {
            var azureConfig = baseConfig as AzureAuthJanitorProviderConfiguration;

            IPagedCollection<IFunctionApp> items;
            if (!string.IsNullOrEmpty(azureConfig.ResourceGroup))
                items = await(await GetAzureAsync()).AppServices.FunctionApps.ListByResourceGroupAsync(azureConfig.ResourceGroup);
            else
                items = await(await GetAzureAsync()).AppServices.FunctionApps.ListAsync();

            return (await Task.WhenAll(items.Select(async i =>
            {
                return (await i.GetAppSettingsAsync()).Select(a =>
                new ProviderResourceSuggestion()
                {
                    Configuration = new AppSettingConfiguration()
                    {
                        ResourceName = i.Name,
                        ResourceGroup = i.ResourceGroupName,
                        SettingName = a.Key
                    },
                    Name = $"Functions/AppSetting - {i.ResourceGroupName} - {i.Name} ({a.Key})",
                    ProviderType = this.GetType().AssemblyQualifiedName,
                    ResourceValues = new[] { a.Value?.Value },
                    AddressableNames = i.EnabledHostNames.ToList()
                });
            }))).SelectMany(f => f).ToList();
        }

        public Task RegisterReferences(IEnumerable<ProviderResourceSuggestion> suggestionsToCheck)
        {
            throw new System.NotImplementedException();
        }
    }
}