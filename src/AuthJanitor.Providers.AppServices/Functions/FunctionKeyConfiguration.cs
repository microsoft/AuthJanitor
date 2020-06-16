// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers.Azure;

namespace AuthJanitor.Providers.AppServices.Functions
{
    /// <summary>
    /// Defines the configuration to rotate a Function Key for an Azure Functions application
    /// </summary>
    public class FunctionKeyConfiguration : AzureAuthJanitorProviderConfiguration
    {
        public const int DEFAULT_KEY_LENGTH = 64;

        /// <summary>
        /// Function name
        /// </summary>
        public string FunctionName { get; set; }

        /// <summary>
        /// Function Key name
        /// </summary>
        public string FunctionKeyName { get; set; }

        /// <summary>
        /// Length of new Function Key
        /// </summary>
        public int KeyLength { get; set; } = DEFAULT_KEY_LENGTH;
    }
}
