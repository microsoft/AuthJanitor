// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AuthJanitor.Integrations.IdentityServices.AzureActiveDirectory
{
    public static class Extensions
    {
        public static void AddAJAzureActiveDirectory<TOptions>(this IServiceCollection serviceCollection, Action<AzureADIdentityServiceConfiguration> configureOptions)
        {
            if (!serviceCollection.Any(c => c.ServiceType == typeof(IHttpContextAccessor)))
            {
                throw new KeyNotFoundException("IHttpContextAccessor service not registered in IServiceCollection!");
            }
            serviceCollection.Configure<AzureADIdentityServiceConfiguration>(configureOptions);
            serviceCollection.AddSingleton<IIdentityService, AzureADIdentityService>();
        }
    }
}
