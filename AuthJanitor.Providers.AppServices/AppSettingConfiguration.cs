// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.ComponentModel;

namespace AuthJanitor.Providers.AppServices
{
    /// <summary>
    /// Defines the configuration to update a consumed AppSetting for an Azure Functions or Azure WebApps application
    /// </summary>
    public class AppSettingConfiguration : SlottableProviderConfiguration
    {
        /// <summary>
        /// AppSetting Name
        /// </summary>
        [DisplayName("Application Setting Name")]
        [Description("Name of AppSetting to update")]
        public string SettingName { get; set; }

        [DisplayName("Commit Connection String")]
        [Description("Commit a Connection String instead of a Key to this AppSetting, when available")]
        public bool CommitAsConnectionString { get; set; }
    }
}
