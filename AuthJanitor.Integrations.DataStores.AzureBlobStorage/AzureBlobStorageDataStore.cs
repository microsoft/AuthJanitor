// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.DataStores;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AuthJanitor.Integrations.DataStores.AzureBlobStorage
{
    public class AzureBlobStorageDataStore<TStoredModel> : IDataStore<TStoredModel> where TStoredModel : IAuthJanitorModel
    {
        private bool _isInitialized = false;
        private readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions()
        {
            Converters = { new TimeSpanConverter() }
        };

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
            await InitializeIfRequired();

            var startedAttempt = DateTimeOffset.UtcNow;
            bool success = false;
            while ((DateTimeOffset.UtcNow - startedAttempt) < TimeSpan.FromSeconds(5))
            {
                try
                {
                    var etag = (await Blob.GetPropertiesAsync())?.Value?.ETag;
                    using (var ms = new MemoryStream())
                    {
                        var serialized = JsonSerializer.Serialize(CachedCollection, _serializerOptions);
                        ms.Write(Encoding.UTF8.GetBytes(serialized));
                        ms.Seek(0, SeekOrigin.Begin);
                        Blob.Upload(ms, conditions: new BlobRequestConditions() { IfMatch = etag });
                        success = true;
                    }
                }
                catch (RequestFailedException)
                {
                }
                catch (Exception ex) { throw ex; }

                if (success)
                    break;

                await Task.Delay(100);
            }
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
                CachedCollection = JsonSerializer.Deserialize<List<TStoredModel>>(str, _serializerOptions);
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