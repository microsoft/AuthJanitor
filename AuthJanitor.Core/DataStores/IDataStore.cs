// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.DataStores;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthJanitor.Integrations.DataStores
{
    public interface IDataStore<TStoredModel> where TStoredModel : IAuthJanitorModel
    {
        /// <summary>
        /// Store an instance of a model which has a new ObjectId
        /// </summary>
        /// <param name="model">Model to store</param>
        Task<TStoredModel> Create(TStoredModel model);

        /// <summary>
        /// Store a new version of a model which already exists
        /// </summary>
        /// <param name="model">Model to store</param>
        Task<TStoredModel> Update(TStoredModel model);

        /// <summary>
        /// List all models currently stored
        /// </summary>
        /// <returns>List of models currently stored</returns>
        Task<ICollection<TStoredModel>> Get();

        /// <summary>
        /// Delete a model by its ObjectId
        /// </summary>
        /// <param name="objectId">ObjectId to delete</param>
        Task Delete(Guid objectId);

        /// <summary>
        /// Test if a given ObjectId has been stored
        /// </summary>
        /// <param name="objectId">ObjectId to test</param>
        /// <returns><c>TRUE</c> if the ObjectId is in the DataStore, otherwise <c>FALSE</c></returns>
        Task<bool> ContainsId(Guid objectId);

        /// <summary>
        /// Retrieve a model by its ObjectId
        /// </summary>
        /// <param name="objectId">ObjectId to retrieve</param>
        /// <returns>Object described by requested ObjectId</returns>
        Task<TStoredModel> GetOne(Guid objectId);

        /// <summary>
        /// Retrieve one or more models, filtered by a Predicate condition
        /// </summary>
        /// <param name="predicate">Predicate used to filter models</param>
        /// <returns>List of models matching predicate</returns>
        Task<ICollection<TStoredModel>> Get(Func<TStoredModel, bool> predicate);

        /// <summary>
        /// Retrieve the first instance of a model, filtered by a Predicate condition
        /// </summary>
        /// <param name="predicate">Predicate used to filter models</param>
        /// <returns>First model matching predicate</returns>
        Task<TStoredModel> GetOne(Func<TStoredModel, bool> predicate);
    }
}
