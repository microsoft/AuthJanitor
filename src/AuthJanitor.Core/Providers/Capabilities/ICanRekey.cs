// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.Capabilities
{
    public interface ICanRekey : IAuthJanitorCapability
    {
        /// <summary>
        /// Call when ready to rekey a given RekeyableService.
        /// </summary>
        /// <param name="requestedValidPeriod">Requested period of validity for new key/secret</param>
        /// <returns></returns>
        Task<RegeneratedSecret> Rekey(TimeSpan requestedValidPeriod);
    }
}
