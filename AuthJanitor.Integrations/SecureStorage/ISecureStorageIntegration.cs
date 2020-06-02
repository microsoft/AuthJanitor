// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
using System.Threading.Tasks;

namespace AuthJanitor.SecureStorage
{
    public interface ISecureStorage
    {
        /// <summary>
        /// Persist sensitive content to secure storage
        /// </summary>
        /// <typeparam name="T">Type of object being persisted</typeparam>
        /// <param name="expiry">Expiry of data</param>
        /// <param name="persistedObject">Object to persist</param>
        /// <returns>ID of persisted object</returns>
        Task<Guid> Persist<T>(DateTimeOffset expiry, T persistedObject);

        /// <summary>
        /// Retrieve sensitive content from secure storage by its ID
        /// </summary>
        /// <typeparam name="T">Type of object being retrieved</typeparam>
        /// <param name="persistenceId">ID of persisted object</param>
        /// <returns>Persisted object</returns>
        Task<T> Retrieve<T>(Guid persistenceId);

        /// <summary>
        /// Destroy a persisted object by its ID
        /// </summary>
        /// <param name="persistenceId">ID of persisted object</param>
        Task Destroy(Guid persistenceId);
    }
}
