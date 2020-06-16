// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers.Azure;
using System.ComponentModel;

namespace AuthJanitor.Providers.AzureSql
{
    public class AzureSqlAdministratorPasswordConfiguration : AzureAuthJanitorProviderConfiguration
    {
        /// <summary>
        /// Password length
        /// </summary>
        [DisplayName("Password length")]
        public int PasswordLength { get; set; }

        /// <summary>
        /// Database name
        /// </summary>
        [DisplayName("Database Name")]
        [Description("Optional database name to embed in connection string")]
        public string DatabaseName { get; set; }
    }
}