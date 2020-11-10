// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.Capabilities
{
    public interface ICanDistributeLongTermSecretValues : IAuthJanitorCapability
    {
        /// <summary>
        /// Call to commit the newly generated secret(s)
        /// </summary>
        Task DistributeLongTermSecretValues(List<RegeneratedSecret> newSecretValues);
    }
}
