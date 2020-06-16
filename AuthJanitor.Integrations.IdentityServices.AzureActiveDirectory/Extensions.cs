// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.IdentityServices;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace AuthJanitor.Integrations.IdentityServices.AzureActiveDirectory
{
    public static class Extensions
    {
        public static void AddAJAzureActiveDirectory<TOptions>(this IServiceCollection serviceCollection, Action<AzureADIdentityServiceConfiguration> configureOptions)
        {
            serviceCollection.Configure<AzureADIdentityServiceConfiguration>(configureOptions);
            serviceCollection.AddSingleton<IIdentityService, AzureADIdentityService>();
        }
    }
}
