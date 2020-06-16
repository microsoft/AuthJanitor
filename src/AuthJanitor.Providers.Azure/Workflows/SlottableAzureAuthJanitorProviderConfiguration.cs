// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.ComponentModel;

namespace AuthJanitor.Providers.Azure.Workflows
{
    public class SlottableAzureAuthJanitorProviderConfiguration : AzureAuthJanitorProviderConfiguration
    {
        public const string DEFAULT_ORIGINAL_SLOT = "production";
        public const string DEFAULT_TEMPORARY_SLOT = "aj-temporary";
        public const string DEFAULT_DESTINATION_SLOT = DEFAULT_ORIGINAL_SLOT;

        /// <summary>
        /// Source Slot (original application)
        /// </summary>
        [DisplayName("Original Application Slot")]
        [Description("Slot to copy settings from, where the app is currently running")]
        public string SourceSlot { get; set; } = DEFAULT_ORIGINAL_SLOT;

        /// <summary>
        /// Temporary Slot (to coalesce new keys/configuration)
        /// </summary>
        [DisplayName("Temporary Application Slot")]
        [Description("Slot to copy settings to, where secrets will be updated prior to switching to destination slot.")]
        public string TemporarySlot { get; set; } = DEFAULT_TEMPORARY_SLOT;

        /// <summary>
        /// Destination Slot (updated application). By default this is the same as the Source Slot.
        /// </summary>
        [DisplayName("Destination Application Slot")]
        [Description("Slot to swap to by the end of the secret rotation process.")]
        public string DestinationSlot { get; set; } = DEFAULT_DESTINATION_SLOT;
    }
}
