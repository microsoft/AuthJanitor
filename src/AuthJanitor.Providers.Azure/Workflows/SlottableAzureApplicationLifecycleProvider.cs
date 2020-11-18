// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers.Capabilities;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.Azure.Workflows
{
    public abstract class SlottableAzureApplicationLifecycleProvider<TConfiguration, TResource> : 
        AzureAuthJanitorProvider<TConfiguration, TResource>,
        ICanRunSanityTests,
        ICanDistributeTemporarySecretValues,
        ICanDistributeLongTermSecretValues,
        ICanPerformUnifiedCommitForTemporarySecretValues,
        ICanPerformUnifiedCommit
        where TConfiguration : SlottableAzureAuthJanitorProviderConfiguration
    {
        protected const string PRODUCTION_SLOT_NAME = "production";
        protected SlottableAzureApplicationLifecycleProvider(ILogger logger) : base(logger) { }

        public async Task Test()
        {
            var resource = await GetResourceAsync();
            if (Configuration.SourceSlot != PRODUCTION_SLOT_NAME)
                await TestSlotAsync(resource, Configuration.SourceSlot);
            if (Configuration.TemporarySlot != PRODUCTION_SLOT_NAME)
                await TestSlotAsync(resource, Configuration.TemporarySlot);
            if (Configuration.DestinationSlot != PRODUCTION_SLOT_NAME)
                await TestSlotAsync(resource, Configuration.DestinationSlot);
        }

        public async Task DistributeTemporarySecretValues(List<RegeneratedSecret> secretValues)
        {
            var resource = await GetResourceAsync();
            await ApplyUpdate(resource, Configuration.TemporarySlot, secretValues);
        }

        public async Task UnifiedCommitForTemporarySecretValues()
        {
            Logger.LogInformation("Swapping settings from {TemporarySlot} to {SourceSlot}",
                Configuration.TemporarySlot,
                Configuration.SourceSlot);
            var resource = await GetResourceAsync();
            await SwapSlotAsync(resource, Configuration.TemporarySlot);
        }

        public async Task DistributeLongTermSecretValues(List<RegeneratedSecret> secretValues)
        {
            var resource = await GetResourceAsync();
            await ApplyUpdate(resource, Configuration.TemporarySlot, secretValues);
        }

        public async Task UnifiedCommit()
        {
            Logger.LogInformation("Swapping from {SourceSlot} to {DestinationSlot}",
                Configuration.TemporarySlot,
                Configuration.DestinationSlot);
            var resource = await GetResourceAsync();
            await SwapSlotAsync(resource, Configuration.TemporarySlot);
        }

        protected abstract Task ApplyUpdate(TResource resource, string slotName, List<RegeneratedSecret> secrets);
        // applies to slot
        protected abstract Task SwapSlotAsync(TResource resource, string sourceSlotName, string destinationSlotName);
        // applies to prod
        protected abstract Task SwapSlotAsync(TResource resource, string sourceSlotName);
        protected abstract Task TestSlotAsync(TResource resource, string slotName);
    }
}
