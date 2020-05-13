// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
using System.Threading.Tasks;

namespace AuthJanitor.Providers
{
    public interface IRekeyableObjectProvider : IAuthJanitorProvider
    {
        /// <summary>
        /// Call before Rekeying occurs to get a secondary secret which will continue
        /// to work while Rekeying is taking place (if any).
        /// </summary>
        /// <returns></returns>
        Task<RegeneratedSecret> GetSecretToUseDuringRekeying();

        /// <summary>
        /// Call when ready to rekey a given RekeyableService.
        /// </summary>
        /// <param name="requestedValidPeriod">Requested period of validity for new key/secret</param>
        /// <returns></returns>
        Task<RegeneratedSecret> Rekey(TimeSpan requestedValidPeriod);

        /// <summary>
        /// Call when the ConsumingApplication has been moved to the RegeneratedKey (from Rekey())
        /// </summary>
        /// <returns></returns>
        Task OnConsumingApplicationSwapped();
    }

    /// <summary>
    /// Describes a service which can have its key(s) rotated
    /// </summary>
    public abstract class RekeyableObjectProvider<TConfiguration> : AuthJanitorProvider<TConfiguration>, IRekeyableObjectProvider where TConfiguration : AuthJanitorProviderConfiguration
    {
        /// <summary>
        /// Call before Rekeying occurs to get a secondary secret which will continue
        /// to work while Rekeying is taking place (if any).
        /// </summary>
        public virtual async Task<RegeneratedSecret> GetSecretToUseDuringRekeying()
        {
            await Task.Yield();
            return null;
        }

        /// <summary>
        /// Call when ready to rekey a given RekeyableService.
        /// </summary>
        /// <param name="requestedValidPeriod">Requested period of validity for new key/secret</param>
        /// <returns></returns>
        public abstract Task<RegeneratedSecret> Rekey(TimeSpan requestedValidPeriod);

        /// <summary>
        /// Call when the ConsumingApplication has been moved to the RegeneratedKey (from Rekey())
        /// </summary>
        /// <returns></returns>
        public virtual Task OnConsumingApplicationSwapped()
        {
            return Task.FromResult(true);
        }
    }
}
