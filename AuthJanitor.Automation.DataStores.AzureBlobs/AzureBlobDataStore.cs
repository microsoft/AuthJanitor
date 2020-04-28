// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Automation.Shared;
using AuthJanitor.Automation.Shared.Models;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthJanitor.Automation.DataStores.AzureBlobs
{
    public class AzureBlobDataStore<TDataType> : IDataStore<TDataType> where TDataType : IAuthJanitorModel
    {
        protected CloudBlockBlob Blob { get; }

        public AzureBlobDataStore(CloudBlockBlob blob)
        {
            Blob = blob;
        }
        public AzureBlobDataStore(string connectionString, string container, string name) :
            this(CloudStorageAccount.Parse(connectionString)
                .CreateCloudBlobClient()
                .GetContainerReference(container)
                .GetBlockBlobReference(name))
        { }

        public Task<bool> ContainsIdAsync(Guid id) =>
            ListAsync().ContinueWith(t => t.Result.Any(o => o.ObjectId == id));

        public async Task CreateAsync(TDataType model)
        {
            var existing = await ListAsync();
            if (existing.Any(o => o.ObjectId == model.ObjectId))
                throw new InvalidOperationException("ID already exists!");
            existing.Add(model);
            await Commit(existing);
        }

        public async Task DeleteAsync(Guid id)
        {
            var existing = await ListAsync();
            if (!existing.Any(o => o.ObjectId == id))
                throw new InvalidOperationException("ID does not exist!");
            existing.RemoveAll(o => o.ObjectId == id);
            await Commit(existing);
        }

        public async Task<TDataType> GetAsync(Guid id)
        {
            if (!await ContainsIdAsync(id))
                throw new InvalidOperationException("ID does not exist!");
            return (await Retrieve()).FirstOrDefault(o => o.ObjectId == id);
        }

        public async Task<List<TDataType>> GetAsync(Func<TDataType, bool> predicate)
        {
            return (await Retrieve()).Where(predicate).ToList();
        }

        public async Task<TDataType> GetOneAsync(Func<TDataType, bool> predicate)
        {
            return (await Retrieve()).Where(predicate).FirstOrDefault();
        }

        public async Task<List<TDataType>> ListAsync()
        {
            return (await Retrieve()).ToList();
        }

        public async Task UpdateAsync(TDataType model)
        {
            var list = await ListAsync();
            if (!list.Any(o => o.ObjectId == model.ObjectId))
                throw new InvalidOperationException("ID does not exist!");

            list.RemoveAll(o => o.ObjectId == model.ObjectId);
            list.Add(model);
            await Commit(list);
        }

        private async Task Commit(List<TDataType> data)
        {
            var leaseId = await Blob.AcquireLeaseAsync(TimeSpan.FromSeconds(30), null);
            await Blob.UploadTextAsync(JsonConvert.SerializeObject(data, Formatting.None));
            await Blob.ReleaseLeaseAsync(new AccessCondition() { LeaseId = leaseId });
        }

        private async Task<List<TDataType>> Retrieve()
        {
            if (!await Blob.ExistsAsync())
            {
                await Commit(new List<TDataType>());
                return new List<TDataType>();
            }
            return JsonConvert.DeserializeObject<List<TDataType>>(await Blob.DownloadTextAsync());
        }
    }
}
