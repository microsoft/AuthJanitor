// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.Azure
{
    public abstract class AzureRekeyableObjectProvider<TConfiguration, TResource> : AzureAuthJanitorProvider<TConfiguration, TResource>, IRekeyableObjectProvider
        where TConfiguration : AzureAuthJanitorProviderConfiguration
    {
        public abstract Task<RegeneratedSecret> GetSecretToUseDuringRekeying();
        public abstract Task OnConsumingApplicationSwapped();
        public abstract Task<RegeneratedSecret> Rekey(TimeSpan requestedValidPeriod);
    }
}
