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
        /// Call to prepare the application for a new secret, passing in a secret
        /// which will be valid while the Rekeying is taking place (for zero-downtime)
        /// </summary>
        Task BeforeRekeying(List<RegeneratedSecret> temporaryUseSecrets);

        /// <summary>
        /// Call to commit the newly generated secret(s)
        /// </summary>
        Task CommitNewSecrets(List<RegeneratedSecret> newSecrets);

        /// <summary>
        /// Call after all new keys have been committed
        /// </summary>
        Task AfterRekeying();
    }

    /// <summary>
    /// Describes an Application Lifecycle Provider which consumes some piece of information to use a Rekeyable Object
    /// </summary>
    public abstract class ApplicationLifecycleProvider<TProviderConfiguration> : AuthJanitorProvider<TProviderConfiguration>, IApplicationLifecycleProvider
        where TProviderConfiguration : AuthJanitorProviderConfiguration
    {
        /// <summary>
        /// Call to prepare the application for a new secret, passing in a secret
        /// which will be valid while the Rekeying is taking place (for zero-downtime)
        /// </summary>
        public virtual Task BeforeRekeying(List<RegeneratedSecret> temporaryUseSecrets)
        {
            return Task.FromResult(true);
        }

        /// <summary>
        /// Call to commit the newly generated secret(s)
        /// </summary>
        public abstract Task CommitNewSecrets(List<RegeneratedSecret> newSecrets);

        /// <summary>
        /// Call after all new keys have been committed
        /// </summary>
        public virtual Task AfterRekeying()
        {
            return Task.FromResult(true);
        }
    }
}
