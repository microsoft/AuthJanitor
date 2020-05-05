// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Integrations.DataStores;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthJanitor.Data.AzureBlobStorage
{
    public class AzureBlobStorageDataStore<TStoredModel> : IDataStore<TStoredModel> where TStoredModel : IAuthJanitorModel
    {
        private const string PREFERRED_LEASE_NAME = "AuthJanitorBlobStorage";

        private bool _isInitialized = false;
        private readonly string _connectionString;
        private readonly string _containerName;
        private readonly string _blobName;

        protected BlockBlobClient Blob { get; private set; }
        protected List<TStoredModel> CachedCollection { get; private set; } = new List<TStoredModel>();

        public AzureBlobStorageDataStore(string connectionString, string container, string name)
        {
            _connectionString = connectionString;
            _containerName = container;
            _blobName = name;
        }

        public async Task<bool> ContainsId(Guid objectId)
        {
            await Cache();

            return CachedCollection.Any(m => m.ObjectId == objectId);
        }

        public async Task<TStoredModel> Create(TStoredModel model)
        {
            await Cache();

            if (CachedCollection.Any(m => m.ObjectId == model.ObjectId))
                throw new InvalidOperationException("Object ID collision!");

            CachedCollection.Add(model);
            await Commit();

            return model;
        }

        public async Task Delete(Guid objectId)
        {
            await Cache();

            if (!CachedCollection.Any(m => m.ObjectId == objectId))
                throw new KeyNotFoundException("Object ID not found!");

            CachedCollection.RemoveAll(c => c.ObjectId == objectId);
            await Commit();
        }

        public async Task<ICollection<TStoredModel>> Get()
        {
            await Cache();

            return new List<TStoredModel>(CachedCollection);
        }

        public async Task<ICollection<TStoredModel>> Get(Func<TStoredModel, bool> whereClause)
        {
            await Cache();

            return new List<TStoredModel>(CachedCollection.Where(whereClause));
        }

        public async Task<TStoredModel> GetOne(Guid objectId)
        {
            await Cache();

            return CachedCollection.FirstOrDefault(c => c.ObjectId == objectId);
        }

        public async Task<TStoredModel> GetOne(Func<TStoredModel, bool> whereClause)
        {
            await Cache();

            return CachedCollection.FirstOrDefault(whereClause);
        }

        public async Task<TStoredModel> Update(TStoredModel model)
        {
            await Cache();

            if (!CachedCollection.Any(m => m.ObjectId == model.ObjectId))
                throw new KeyNotFoundException("Object ID not found!");

            CachedCollection.RemoveAll(c => c.ObjectId == model.ObjectId);
            CachedCollection.Add(model);
            await Commit();

            return model;
        }

        private Task Commit()
        {
            lock (CachedCollection)
            {
                return WithLease(TimeSpan.FromSeconds(15), async (lease) =>
                {
                    var serialized = JsonConvert.SerializeObject(CachedCollection, Formatting.None);
                    using (var ms = new MemoryStream())
                    {
                        await ms.WriteAsync(Encoding.UTF8.GetBytes(serialized));
                        ms.Seek(0, SeekOrigin.Begin);
                        await Blob.UploadAsync(ms);
                    }
                });
            }
        }

        private Task Cache()
        {
            lock (CachedCollection)
            {
                return WithLease(TimeSpan.FromSeconds(15), async (lease) =>
                {
                    var blobText = await Blob.DownloadAsync(conditions: new BlobRequestConditions() { LeaseId = lease.LeaseId });
                    using (var ms = new MemoryStream())
                    {
                        await blobText.Value.Content.CopyToAsync(ms);
                        ms.Seek(0, SeekOrigin.Begin);
                        var str = Encoding.UTF8.GetString(ms.ToArray());
                        CachedCollection = JsonConvert.DeserializeObject<List<TStoredModel>>(str);
                    }
                });
            }
        }

        private async Task WithLease(TimeSpan leaseTime, Action<BlobLease> action)
        {
            if (!_isInitialized)
            {
                var blobServiceClient = new BlobServiceClient(_connectionString);

                var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
                await containerClient.CreateIfNotExistsAsync();

                Blob = containerClient.GetBlockBlobClient(_blobName);
                if (!await Blob.ExistsAsync())
                {
                    using (var ms = new MemoryStream())
                    {
                        ms.Write(Encoding.UTF8.GetBytes("[]"));
                        ms.Seek(0, SeekOrigin.Begin);
                        await Blob.UploadAsync(ms);
                    }
                    CachedCollection = new List<TStoredModel>();
                }
                _isInitialized = true;
            }

            var leaseClient = Blob.GetBlobLeaseClient(PREFERRED_LEASE_NAME);
            BlobLease lease = null;
            try
            {
                while (lease == null || string.IsNullOrEmpty(lease.LeaseId))
                {
                    try
                    {
                        lease = await leaseClient.AcquireAsync(leaseTime);
                        if (lease == null || string.IsNullOrEmpty(lease.LeaseId))
                            await Task.Delay(TimeSpan.FromMilliseconds(new Random((int)DateTime.Now.Ticks).Next(250, 1000)));
                    }
                    catch (RequestFailedException ex)
                    {
                        if (ex.Status != 409)
                            throw;
                    }
                }

                action(lease);
            }
            finally
            {
                if (lease != null && !string.IsNullOrEmpty(lease.LeaseId))
                    await leaseClient.ReleaseAsync();
            }
        }
    }
}