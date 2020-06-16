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
    [Provider(Name = "WebApp - Connection String",
              IconClass = "fa fa-globe",
              Description = "Manages the lifecycle of an Azure Web App which reads from a Connection String",
              Features = ProviderFeatureFlags.CanRotateWithoutDowntime |
                         ProviderFeatureFlags.IsTestable)]
    [ProviderImage(ProviderImages.WEBAPPS_SVG)]
    public class ConnectionStringWebAppApplicationLifecycleProvider : SlottableAzureApplicationLifecycleProvider<ConnectionStringConfiguration, IWebApp>
    {
        private readonly ILogger _logger;

        public ConnectionStringWebAppApplicationLifecycleProvider(ILogger<ConnectionStringWebAppApplicationLifecycleProvider> logger) : base(logger)
        {
            _logger = logger;
        }

        protected override async Task ApplyUpdate(IWebApp resource, string slotName, List<RegeneratedSecret> secrets)
        {
            // NOTE: This is safe, but the type structure is weird to reproduce without a lot of otherwise pointless generics
            dynamic updateBase = (await resource.DeploymentSlots.GetByNameAsync(slotName)).Update();
            foreach (RegeneratedSecret secret in secrets)
            {
                var connectionStringName = string.IsNullOrEmpty(secret.UserHint) ? Configuration.ConnectionStringName : $"{Configuration.ConnectionStringName}-{secret.UserHint}";
                _logger.LogInformation("Updating Connection String '{ConnectionStringName}' in slot '{SlotName}'", connectionStringName, slotName);
                updateBase = updateBase.WithoutConnectionString(connectionStringName);
                updateBase = updateBase.WithConnectionString(connectionStringName, secret.NewConnectionStringOrKey, Configuration.ConnectionStringType);
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
            $"Populates a Connection String for '{Configuration.ConnectionStringType}' called " +
            $"'{Configuration.ConnectionStringName}' in an Azure " +
            $"Web Application called {Configuration.ResourceName} (Resource Group " +
            $"'{Configuration.ResourceGroup}'). During the rekeying, the Functions App will " +
            $"be moved from slot '{Configuration.SourceSlot}' to slot '{Configuration.TemporarySlot}' " +
            $"temporarily, and then to slot '{Configuration.DestinationSlot}'.";
    }
}
