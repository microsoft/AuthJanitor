// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.Threading.Tasks;

namespace AuthJanitor.Providers.Capabilities
{
    public interface ICanCleanup : IAuthJanitorCapability
    {
        Task Cleanup();
    }
}