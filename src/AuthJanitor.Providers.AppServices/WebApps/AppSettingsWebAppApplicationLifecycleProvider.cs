// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
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

namespace AuthJanitor.Providers.AppServices.WebApps
{
    [Provider(Name = "WebApp - AppSettings",
              Description = "Manages the lifecycle of an Azure Web App which reads a Managed Secret from its Application Settings",
              SvgImage = ProviderImages.WEBAPPS_SVG)]
    public class AppSettingsWebAppApplicationLifecycleProvider : 
        SlottableAzureApplicationLifecycleProvider<AppSettingConfiguration, IWebApp>,
        ICanEnumerateResourceCandidates
    {
        private readonly ILogger _logger;

        public AppSettingsWebAppApplicationLifecycleProvider(ILogger<AppSettingsWebAppApplicationLifecycleProvider> logger) : base(logger)
        {
            _logger = logger;
        }

        protected override async Task ApplyUpdate(IWebApp resource, string slotName, List<RegeneratedSecret> secrets)
        {
            // NOTE: This is safe, but the type structure is weird to reproduce without a lot of otherwise pointless generics
            dynamic updateBase = (await resource.DeploymentSlots.GetByNameAsync(slotName)).Update();
            foreach (RegeneratedSecret secret in secrets)
            {
                var appSettingName = string.IsNullOrEmpty(secret.UserHint) ? Configuration.SettingName : $"{Configuration.SettingName}-{secret.UserHint}";
                _logger.LogInformation("Updating AppSetting '{AppSettingName}' in slot '{SlotName}' (as {AppSettingType})", appSettingName, slotName,
                    Configuration.CommitAsConnectionString ? "connection string" : "secret");

                updateBase = updateBase.WithAppSetting(appSettingName,
                    Configuration.CommitAsConnectionString ? secret.NewConnectionStringOrKey : secret.NewSecretValue);
            }
            await updateBase.ApplyAsync();
        }

        protected override async Task SwapSlotAsync(IWebApp resource, string sourceSlotName) =>
            await resource.SwapAsync(sourceSlotName);

        protected override async Task SwapSlotAsync(IWebApp resource, string sourceSlotName, string destinationSlotName) =>
            await (await resource.DeploymentSlots.GetByNameAsync(destinationSlotName))
                .SwapAsync(sourceSlotName);

        protected override ISupportsGettingByResourceGroup<IWebApp> GetResourceCollection(IAzure azure) => azure.AppServices.WebApps;

        protected override async Task TestSlotAsync(IWebApp resource, string slotName)
        {
            var slot = await resource.DeploymentSlots.GetByNameAsync(slotName);
            if (slot == null) throw new System.Exception($"Slot {slotName} not found");
        }

        public override string GetDescription() =>
            $"Populates an App Setting called '{Configuration.SettingName}' in an Azure " +
            $"Web Application called {Configuration.ResourceName} (Resource Group " +
            $"'{Configuration.ResourceGroup}'). During the rekeying, the Functions App will " +
            $"be moved from slot '{Configuration.SourceSlot}' to slot '{Configuration.TemporarySlot}' " +
            $"temporarily, and then back.";

        public async Task<List<ProviderResourceSuggestion>> EnumerateResourceCandidates(AuthJanitorProviderConfiguration baseConfig)
        {
            var azureConfig = baseConfig as AzureAuthJanitorProviderConfiguration;

            IPagedCollection<IWebApp> items;
            if (!string.IsNullOrEmpty(azureConfig.ResourceGroup))
                items = await (await GetAzureAsync()).AppServices.WebApps.ListByResourceGroupAsync(azureConfig.ResourceGroup);
            else
                items = await (await GetAzureAsync()).AppServices.WebApps.ListAsync();

            return (await Task.WhenAll(items.Select(async i =>
            {
                return (await i.GetAppSettingsAsync()).Select(c =>
                new ProviderResourceSuggestion()
                {
                    Configuration = new AppSettingConfiguration()
                    {
                        ResourceName = i.Name,
                        ResourceGroup = i.ResourceGroupName,
                        // TODO: CommitAsConnectionString?
                        SettingName = c.Key
                    },
                    Name = $"WebApp/AppSettings - {i.ResourceGroupName} - {i.Name}",
                    ProviderType = this.GetType().AssemblyQualifiedName,
                    ResourceValues = new[] { c.Value?.Value },
                    AddressableNames = i.EnabledHostNames.ToList()
                });
            }))).SelectMany(f => f).ToList();
        }
    }
}
