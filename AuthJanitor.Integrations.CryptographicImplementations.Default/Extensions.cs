// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.CryptographicImplementations;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace AuthJanitor.Integrations.CryptographicImplementations.Default
{
    public static class Extensions
    {
        public static void AddAJDefaultCryptographicImplementation<TOptions>(this IServiceCollection serviceCollection, Action<DefaultCryptographicImplementationConfiguration> configureOptions)
        {
            serviceCollection.Configure<DefaultCryptographicImplementationConfiguration>(configureOptions);
            serviceCollection.AddSingleton<ICryptographicImplementation, DefaultCryptographicImplementation>();
        }
    }
}
