// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
namespace AuthJanitor.Providers
{
    /// <summary>
    /// Describes the configuration of an Extension
    /// </summary>
    public abstract class AuthJanitorProviderConfiguration
    {
        /// <summary>
        /// Arbitrary user-specified hint string (from Provider Configuration) used to distinguish among multiple 
        /// RegeneratedSecrets entering an ApplicationLifecycleProvider
        /// </summary>
        public string UserHint { get; set; }

        /// <summary>
        /// Creates a HashCode for this configuration based on common resources.
        /// This is used to group items for parallelization and unified commits.
        /// For example, this might be a ResourceGroup/ResourceName.
        /// </summary>
        public abstract int GenerateResourceIdentifierHashCode();
    }
}
