// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;

namespace AuthJanitor.Providers
{
    /// <summary>
    /// Describes a newly generated key/connection string
    /// </summary>
    public class RegeneratedSecret
    {
        /// <summary>
        /// Newly generated key
        /// </summary>
        public string NewSecretValue { get; set; }

        /// <summary>
        /// Newly generated connection string, if appropriate
        /// </summary>
        public string NewConnectionString { get; set; }

        /// <summary>
        /// Retrieve either the Connection String, or if that is not specified, the newly generated Key
        /// </summary>
        public string NewConnectionStringOrKey => string.IsNullOrEmpty(NewConnectionString) ? NewSecretValue : NewConnectionString;

        /// <summary>
        /// Arbitrary user-specified hint string (from Provider Configuration) used to distinguish among multiple 
        /// RegeneratedSecrets entering an ApplicationLifecycleProvider
        /// </summary>
        public string UserHint { get; set; }

        /// <summary>
        /// If the RekeyableService controls the generated key's expiry, it is stored here
        /// </summary>
        public DateTimeOffset Expiry { get; set; } = DateTimeOffset.MinValue;

        /// <summary>
        /// Provider Configuration
        /// </summary>
        public AuthJanitorProviderConfiguration ProviderConfiguration { get; set; }
    }
}
