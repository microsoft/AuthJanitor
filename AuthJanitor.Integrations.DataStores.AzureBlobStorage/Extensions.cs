// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.UI.Shared.Models;
using AuthJanitor.DataStores;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace AuthJanitor.Integrations.DataStores.AzureBlobStorage
{
    public static class Extensions
    {
        public static void AddAJAzureBlobStorage<TStoredModel, TOptions>(this IServiceCollection serviceCollection, Action<AzureBlobStorageDataStoreConfiguration> configureOptions)
            where TStoredModel : IAuthJanitorModel
        {
            serviceCollection.Configure<AzureBlobStorageDataStoreConfiguration>(configureOptions);
            serviceCollection.AddAJAzureBlobStorage<TStoredModel>();
        }

        public static void AddAJAzureBlobStorage<TOptions>(this IServiceCollection serviceCollection, Action<AzureBlobStorageDataStoreConfiguration> configureOptions)
        {
            serviceCollection.Configure<AzureBlobStorageDataStoreConfiguration>(configureOptions);
            serviceCollection.AddSingleton<IDataStore<ManagedSecret>, AzureBlobStorageDataStore<ManagedSecret>>();
            serviceCollection.AddSingleton<IDataStore<RekeyingTask>, AzureBlobStorageDataStore<RekeyingTask>>();
            serviceCollection.AddSingleton<IDataStore<Resource>, AzureBlobStorageDataStore<Resource>>();
            serviceCollection.AddSingleton<IDataStore<ScheduleWindow>, AzureBlobStorageDataStore<ScheduleWindow>>();
        }

        public static void AddAJAzureBlobStorage<TStoredModel>(this IServiceCollection serviceCollection)
            where TStoredModel : IAuthJanitorModel
        {
            serviceCollection.AddSingleton<IDataStore<TStoredModel>, AzureBlobStorageDataStore<TStoredModel>>();
        }
    }
}