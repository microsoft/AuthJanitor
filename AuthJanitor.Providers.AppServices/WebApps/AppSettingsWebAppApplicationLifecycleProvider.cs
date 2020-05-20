// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers.Azure.Workflows;
using Microsoft.Azure.Management.AppService.Fluent;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core.CollectionActions;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.AppServices.WebApps
{
    [Provider(Name = "WebApp - AppSettings",
              IconClass = "fa fa-globe",
              Description = "Manages the lifecycle of an Azure Web App which reads a Managed Secret from its Application Settings",
              Features = ProviderFeatureFlags.CanRotateWithoutDowntime |
                         ProviderFeatureFlags.IsTestable)]
    [ProviderImage(ProviderImages.WEBAPPS_SVG)]
    public class AppSettingsWebAppApplicationLifecycleProvider : SlottableAzureApplicationLifecycleProvider<AppSettingConfiguration, IWebApp>
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

        protected override Task SwapSlotAsync(IWebApp resource, string slotName) => resource.SwapAsync(slotName);

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
            $"temporarily, and then to slot '{Configuration.DestinationSlot}'.";
    }
}
