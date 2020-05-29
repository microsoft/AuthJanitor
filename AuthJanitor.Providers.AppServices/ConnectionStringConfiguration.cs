// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers.Azure.Workflows;
using Microsoft.Azure.Management.AppService.Fluent.Models;
using System.ComponentModel;

namespace AuthJanitor.Providers.AppServices
{
    /// <summary>
    /// Defines the configuration to update a consumed Connection String for an Azure Functions or Azure WebApps application
    /// </summary>
    public class ConnectionStringConfiguration : SlottableAzureAuthJanitorProviderConfiguration
    {
        /// <summary>
        /// Connection String name
        /// </summary>
        [DisplayName("Connection String Name")]
        [Description("Name of Connection String to update")]
        public string ConnectionStringName { get; set; }

        /// <summary>
        /// Connection String type
        /// </summary>
        [DisplayName("Connection String Type")]
        [Description("Type of Connection String in configuration")]
        public ConnectionStringType ConnectionStringType { get; set; }
    }
}
