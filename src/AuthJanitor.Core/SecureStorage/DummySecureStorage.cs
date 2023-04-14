// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace AuthJanitor.SecureStorage
{
    public static class DummySecureStorageExtensions
    {
        public static void AddDummySecureStorage(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<ISecureStorage, DummySecureStorage>();
        }
    }
    public class DummySecureStorage : ISecureStorage
    {
        public Task Destroy(Guid persistenceId) => Task.FromResult(false);

        public Task<Guid> Persist<T>(DateTimeOffset expiry, T persistedObject) => Task.FromResult(Guid.Empty);

        public Task<T> Retrieve<T>(Guid persistenceId) => Task.FromResult(default(T));
    }
}
