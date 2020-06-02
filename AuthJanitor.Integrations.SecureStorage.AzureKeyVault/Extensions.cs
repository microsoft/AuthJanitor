// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.SecureStorage;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace AuthJanitor.Integrations.SecureStorage.AzureKeyVault
{
    public static class Extensions
    {
        public static void AddAJAzureKeyVault<TOptions>(this IServiceCollection serviceCollection, Action<KeyVaultSecureStorageProviderConfiguration> configureOptions)
        {
            serviceCollection.Configure<KeyVaultSecureStorageProviderConfiguration>(configureOptions);
            serviceCollection.AddSingleton<ISecureStorage, KeyVaultSecureStorageProvider>();
        }
    }
}
