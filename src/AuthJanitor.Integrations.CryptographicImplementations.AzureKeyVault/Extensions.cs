// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Microsoft.Extensions.DependencyInjection;
using System;

namespace AuthJanitor.Integrations.CryptographicImplementations.AzureKeyVault
{
    public static class Extensions
    {
        public static void AddAJKeyVaultCryptographicImplementation<TOptions>(this IServiceCollection serviceCollection, Action<AzureKeyVaultCryptographicImplementationConfiguration> configureOptions)
        {
            serviceCollection.Configure<AzureKeyVaultCryptographicImplementationConfiguration>(configureOptions);
            serviceCollection.AddSingleton<ICryptographicImplementation, AzureKeyVaultCryptographicImplementation>();
        }
    }
}
