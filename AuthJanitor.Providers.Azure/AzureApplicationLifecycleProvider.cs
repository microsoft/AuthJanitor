// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.Azure
{
    public abstract class AzureApplicationLifecycleProvider<TConfiguration, TResource> : AzureAuthJanitorProvider<TConfiguration, TResource>, IApplicationLifecycleProvider
        where TConfiguration : AzureAuthJanitorProviderConfiguration
    {
        public abstract Task AfterRekeying();
        public abstract Task BeforeRekeying(List<RegeneratedSecret> temporaryUseSecrets);
        public abstract Task CommitNewSecrets(List<RegeneratedSecret> newSecrets);
    }
}
