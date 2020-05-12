// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AuthJanitor.Integrations.DataStores.AzureBlobStorage
{
    public class AzureBlobStorageDataStore<TStoredModel> : IDataStore<TStoredModel> where TStoredModel : IAuthJanitorModel
    {
        private bool _isInitialized = false;

        private AzureBlobStorageDataStoreConfiguration Configuration { get; }

        protected BlockBlobClient Blob { get; private set; }

        protected List<TStoredModel> CachedCollection { get; private set; } = new List<TStoredModel>();

        public AzureBlobStorageDataStore(
            IOptions<AzureBlobStorageDataStoreConfiguration> configuration)
        {
            Configuration = configuration.Value;
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

        private async Task Commit()
        {
            await RunLeaseInterlockedAction(() =>
            {
                using (var ms = new MemoryStream())
                {
                    var serialized = JsonConvert.SerializeObject(CachedCollection, Formatting.None);
                    ms.Write(Encoding.UTF8.GetBytes(serialized));
                    ms.Seek(0, SeekOrigin.Begin);
                    Blob.Upload(ms);
                }
            });
        }

        private async Task Cache()
        {
            await InitializeIfRequired();

            var blobText = Blob.Download();
            using (var ms = new MemoryStream())
            {
                blobText.Value.Content.CopyTo(ms);
                ms.Seek(0, SeekOrigin.Begin);
                var str = Encoding.UTF8.GetString(ms.ToArray());
                CachedCollection = JsonConvert.DeserializeObject<List<TStoredModel>>(str);
            }
        }

        private static TimeSpan LEASE_ABANDON_TIME = TimeSpan.FromSeconds(60);
        private static TimeSpan MAX_ATTEMPT_TIME = TimeSpan.FromSeconds(30);
        private const int MIN_BACKOFF_TIME_MS = 750;
        private const int MAX_BACKOFF_TIME_MS = 2500;
        private static Random _rng = new Random((int)DateTimeOffset.UtcNow.Ticks);

        private const string METADATA_TAG = "ajUpdate";

        private async Task RunLeaseInterlockedAction(Action action)
        {
            await InitializeIfRequired();

            var startedAttempt = DateTimeOffset.UtcNow;
            bool success = false;
            while ((DateTimeOffset.UtcNow - startedAttempt) < MAX_ATTEMPT_TIME)
            {
                var metadata = (await Blob.GetPropertiesAsync())?.Value?.Metadata;
                if (metadata == null)
                {
                    // Could be broken, could be system busy
                    await Task.Delay(_rng.Next(MIN_BACKOFF_TIME_MS, MAX_BACKOFF_TIME_MS));
                    continue;
                }

                if (metadata.ContainsKey(METADATA_TAG) &&
                    !string.IsNullOrEmpty(metadata[METADATA_TAG]))
                {
                    // Lease exists
                    var leaseStarted = DateTimeOffset.Parse(metadata[METADATA_TAG]);
                    if ((DateTimeOffset.UtcNow - leaseStarted) < LEASE_ABANDON_TIME)
                    {
                        // Lease has not yet been abandoned
                        await Task.Delay(_rng.Next(MIN_BACKOFF_TIME_MS, MAX_BACKOFF_TIME_MS));
                        continue;
                    }
                }

                // Set lease tag
                await Blob.SetMetadataAsync(new Dictionary<string, string>()
                {
                    { METADATA_TAG, DateTimeOffset.UtcNow.ToString() }
                });

                // Action!
                try
                {
                    action();
                    success = true;
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                finally
                {
                    // Release
                    await Blob.SetMetadataAsync(new Dictionary<string, string>()
                    {
                        { METADATA_TAG, string.Empty }
                    });
                }
            }
            if (!success)
            {
                throw new Exception("Could not acquire lease in time!");
            }
        }

        private async Task InitializeIfRequired()
        {
            if (!_isInitialized)
            {
                var blobServiceClient = new BlobServiceClient(Configuration.ConnectionString);

                var containerClient = blobServiceClient.GetBlobContainerClient(Configuration.Container);
                await containerClient.CreateIfNotExistsAsync();

                Blob = containerClient.GetBlockBlobClient(typeof(TStoredModel).Name.ToLower());
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
        }
    }
}