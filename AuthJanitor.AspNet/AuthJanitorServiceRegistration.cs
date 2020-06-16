// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.UI.Shared.MetaServices;
using Microsoft.Extensions.DependencyInjection;

namespace AuthJanitor.UI.Shared
{
    public static class AuthJanitorServiceRegistration
    {
        public static void RegisterServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<EventDispatcherMetaService>();
            serviceCollection.AddSingleton<TaskExecutionMetaService>();
        }
    }
}
