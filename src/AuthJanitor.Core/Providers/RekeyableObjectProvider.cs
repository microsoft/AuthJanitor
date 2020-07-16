// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
using System.Threading.Tasks;

namespace AuthJanitor.Providers
{
    public interface IRekeyableObjectProvider : IAuthJanitorProvider
    {
        /// <summary>
        /// Call when ready to rekey a given RekeyableService.
        /// </summary>
        /// <param name="requestedValidPeriod">Requested period of validity for new key/secret</param>
        /// <returns></returns>
        Task<RegeneratedSecret> Rekey(TimeSpan requestedValidPeriod);
    }

    /// <summary>
    /// Describes a service which can have its key(s) rotated
    /// </summary>
    public abstract class RekeyableObjectProvider<TConfiguration> : AuthJanitorProvider<TConfiguration>, IRekeyableObjectProvider where TConfiguration : AuthJanitorProviderConfiguration
    {
        /// <summary>
        /// Call when ready to rekey a given RekeyableService.
        /// </summary>
        /// <param name="requestedValidPeriod">Requested period of validity for new key/secret</param>
        /// <returns></returns>
        public abstract Task<RegeneratedSecret> Rekey(TimeSpan requestedValidPeriod);
    }
}
