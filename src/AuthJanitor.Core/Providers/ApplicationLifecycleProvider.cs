// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthJanitor.Providers
{
    /// <summary>
    /// Describes an Application Lifecycle Provider which consumes some piece of information to use a Rekeyable Object
    /// </summary>
    public interface IApplicationLifecycleProvider : IAuthJanitorProvider
    {
        /// <summary>
        /// Call to commit the newly generated secret(s)
        /// </summary>
        Task DistributeLongTermSecretValues(List<RegeneratedSecret> newSecretValues);
    }

    /// <summary>
    /// Describes an Application Lifecycle Provider which consumes some piece of information to use a Rekeyable Object
    /// </summary>
    public abstract class ApplicationLifecycleProvider<TProviderConfiguration> : AuthJanitorProvider<TProviderConfiguration>, IApplicationLifecycleProvider
        where TProviderConfiguration : AuthJanitorProviderConfiguration
    {
        /// <summary>
        /// Call to commit the newly generated secret(s)
        /// </summary>
        public abstract Task DistributeLongTermSecretValues(List<RegeneratedSecret> newSecretValues);
    }
}
