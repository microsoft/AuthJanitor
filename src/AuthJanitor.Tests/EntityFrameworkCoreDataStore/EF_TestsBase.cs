// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.DataStores;
using AuthJanitor.Integrations.DataStores.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AuthJanitor.Tests.EntityFrameworkCoreDataStore
{
    public abstract class EF_TestsBase<TStoredModel> where TStoredModel : class, IAuthJanitorModel
    {
        private AuthJanitorDbContext dbContext = null;
        protected TStoredModel model = null;

        protected EntityFrameworkCoreDataStore<TStoredModel> GetDataStore()
        {
            if (model == null)
                model = CreateModel();
            if (dbContext == null)
                dbContext = new AuthJanitorDbContext(new DbContextOptionsBuilder<AuthJanitorDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            return new EntityFrameworkCoreDataStore<TStoredModel>(dbContext);
        }

        protected Task CleanModel()
        {
            dbContext.Set<TStoredModel>().RemoveRange(dbContext.Set<TStoredModel>().ToArray());
            return dbContext.SaveChangesAsync();
        }

        protected abstract TStoredModel CreateModel();
        protected abstract TStoredModel UpdatedModel();
        protected abstract bool CompareModel(TStoredModel model1, TStoredModel model2);

        [Fact]
        public async Task ValidatesCreate()
        {
            var datastore = GetDataStore();
            var storedModel = await datastore.Create(model, CancellationToken.None);
            Assert.True(CompareModel(model, storedModel));
            var fetchedModel = await datastore.GetOne(model.ObjectId, CancellationToken.None);
            Assert.True(CompareModel(model, fetchedModel));
            await CleanModel();
        }

        [Fact]
        public async Task ValidatesUpdate()
        {
            var datastore = GetDataStore();
            await datastore.Create(model, CancellationToken.None);
            var updatedModel = UpdatedModel();
            var storedModel = await datastore.Update(updatedModel, CancellationToken.None);
            Assert.True(CompareModel(updatedModel, storedModel));
            var fetchedModel = await datastore.GetOne(model.ObjectId, CancellationToken.None);
            Assert.True(CompareModel(updatedModel, fetchedModel));
            await CleanModel();
        }

        [Fact]
        public async Task ValidatesGet()
        {
            var datastore = GetDataStore();
            await datastore.Create(model, CancellationToken.None);
            var models = await datastore.Get(CancellationToken.None);
            Assert.Equal(1, models.Count);
            Assert.True(CompareModel(model, models.First()));
            await CleanModel();
        }

        [Fact]
        public async Task ValidatesGetWithPredicateReturnsModelResult()
        {
            var datastore = GetDataStore();
            await datastore.Create(model, CancellationToken.None);
            var models = await datastore.Get(x => x.ObjectId == model.ObjectId, CancellationToken.None);
            Assert.Equal(1, models.Count);
            Assert.True(CompareModel(model, models.First()));
            await CleanModel();
        }

        [Fact]
        public async Task ValidatesGetWithPredicateReturnsNoResult()
        {
            var datastore = GetDataStore();
            await datastore.Create(model, CancellationToken.None);
            var models = await datastore.Get(x => x.ObjectId == Guid.NewGuid(), CancellationToken.None);
            Assert.Equal(0, models.Count);
            await CleanModel();
        }

        [Fact]
        public async Task ValidatesGetOne()
        {
            var datastore = GetDataStore();
            await datastore.Create(model, CancellationToken.None);
            var modelOne = await datastore.GetOne(model.ObjectId, CancellationToken.None);
            Assert.True(CompareModel(model, modelOne));
            await CleanModel();
        }

        [Fact]
        public async Task ValidatesGetOneWithPredicateReturnsModelResult()
        {
            var datastore = GetDataStore();
            await datastore.Create(model, CancellationToken.None);
            var modelOne = await datastore.GetOne(x => x.ObjectId == model.ObjectId, CancellationToken.None);
            Assert.True(CompareModel(model, modelOne));
            await CleanModel();
        }

        [Fact]
        public async Task ValidatesGetOneWithPredicateReturnsNoResult()
        {
            var datastore = GetDataStore();
            await datastore.Create(model, CancellationToken.None);
            var modelOne = await datastore.GetOne(x => x.ObjectId == Guid.NewGuid(), CancellationToken.None);
            Assert.Null(modelOne);
            await CleanModel();
        }

        [Fact]
        public async Task ValidatesContainsId()
        {
            var datastore = GetDataStore();
            await datastore.Create(model, CancellationToken.None);
            var exists = await datastore.ContainsId(model.ObjectId, CancellationToken.None);
            Assert.True(exists);
            await CleanModel();
        }

        [Fact]
        public async Task ValidatesDelete()
        {
            var datastore = GetDataStore();
            await datastore.Create(model, CancellationToken.None);
            await datastore.Delete(model.ObjectId, CancellationToken.None);
            var models = await datastore.Get(CancellationToken.None);
            Assert.Equal(0, models.Count);
            await CleanModel();
        }
    }
}
