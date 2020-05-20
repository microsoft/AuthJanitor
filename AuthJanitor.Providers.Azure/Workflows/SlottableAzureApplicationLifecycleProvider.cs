// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.Azure.Workflows
{
    public abstract class SlottableAzureApplicationLifecycleProvider<TConfiguration, TResource> : AzureApplicationLifecycleProvider<TConfiguration, TResource>
        where TConfiguration : SlottableAzureAuthJanitorProviderConfiguration
    {
        private const string PRODUCTION_SLOT_NAME = "production";
        private readonly ILogger _logger;
        protected SlottableAzureApplicationLifecycleProvider(ILogger logger) => _logger = logger;

        public override async Task Test()
        {
            var resource = await GetResourceAsync();
            if (Configuration.SourceSlot != PRODUCTION_SLOT_NAME)
                await TestSlotAsync(resource, Configuration.SourceSlot);
            if (Configuration.TemporarySlot != PRODUCTION_SLOT_NAME)
                await TestSlotAsync(resource, Configuration.TemporarySlot);
            if (Configuration.DestinationSlot != PRODUCTION_SLOT_NAME)
                await TestSlotAsync(resource, Configuration.DestinationSlot);
        }

        public override async Task AfterRekeying()
        {
            _logger.LogInformation("Swapping to '{SlotName}'", Configuration.TemporarySlot);
            await SwapSlotAsync(await GetResourceAsync(), Configuration.TemporarySlot);
            _logger.LogInformation("Swap complete!");
        }

        public override async Task BeforeRekeying(List<RegeneratedSecret> temporaryUseSecrets)
        {
            await ApplySecretSwap(temporaryUseSecrets);
            _logger.LogInformation("BeforeRekeying completed!");
        }

        public override async Task CommitNewSecrets(List<RegeneratedSecret> newSecrets)
        {
            await ApplySecretSwap(newSecrets);
            _logger.LogInformation("CommitNewSecrets completed!");
        }

        protected abstract Task ApplyUpdate(TResource resource, string slotName, List<RegeneratedSecret> secrets);
        protected abstract Task SwapSlotAsync(TResource resource, string slotName);
        protected abstract Task TestSlotAsync(TResource resource, string slotName);
        
        private async Task ApplySecretSwap(List<RegeneratedSecret> secrets)
        {
            var resource = await GetResourceAsync();
            await ApplyUpdate(resource, Configuration.TemporarySlot, secrets);

            _logger.LogInformation("Swapping to '{SlotName}'", Configuration.TemporarySlot);
            await SwapSlotAsync(resource, Configuration.TemporarySlot);
        }
    }
}
