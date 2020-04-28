// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Automation.Shared.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthJanitor.Automation.Shared
{
    public interface IDataStore<TModel> where TModel : IAuthJanitorModel
    {
        /// <summary>
        /// Store an instance of a model which has a new ObjectId
        /// </summary>
        /// <param name="model">Model to store</param>
        Task CreateAsync(TModel model);

        /// <summary>
        /// Store a new version of a model which already exists
        /// </summary>
        /// <param name="model">Model to store</param>
        Task UpdateAsync(TModel model);

        /// <summary>
        /// List all models currently stored
        /// </summary>
        /// <returns>List of models currently stored</returns>
        Task<List<TModel>> ListAsync();

        /// <summary>
        /// Delete a model by its ObjectId
        /// </summary>
        /// <param name="objectId">ObjectId to delete</param>
        Task DeleteAsync(Guid objectId);

        /// <summary>
        /// Test if a given ObjectId has been stored
        /// </summary>
        /// <param name="objectId">ObjectId to test</param>
        /// <returns><c>TRUE</c> if the ObjectId is in the DataStore, otherwise <c>FALSE</c></returns>
        Task<bool> ContainsIdAsync(Guid objectId);

        /// <summary>
        /// Retrieve a model by its ObjectId
        /// </summary>
        /// <param name="objectId">ObjectId to retrieve</param>
        /// <returns>Object described by requested ObjectId</returns>
        Task<TModel> GetAsync(Guid objectId);

        /// <summary>
        /// Retrieve one or more models, filtered by a Predicate condition
        /// </summary>
        /// <param name="predicate">Predicate used to filter models</param>
        /// <returns>List of models matching predicate</returns>
        Task<List<TModel>> GetAsync(Func<TModel, bool> predicate);

        /// <summary>
        /// Retrieve the first instance of a model, filtered by a Predicate condition
        /// </summary>
        /// <param name="predicate">Predicate used to filter models</param>
        /// <returns>First model matching predicate</returns>
        Task<TModel> GetOneAsync(Func<TModel, bool> predicate);
    }
}
