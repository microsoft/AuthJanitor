// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.DataStores;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AuthJanitor.Integrations.DataStores.EntityFrameworkCore
{
    public class EntityFrameworkCoreDataStore<TStoredModel> : IDataStore<TStoredModel> where TStoredModel : class, IAuthJanitorModel
    {
        private readonly AuthJanitorDbContext dbContext;

        private DbSet<TStoredModel> Set
        {
            get
            {
                return dbContext.Set<TStoredModel>();
            }
        }

        public EntityFrameworkCoreDataStore(AuthJanitorDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        public async Task<bool> ContainsId(Guid objectId, CancellationToken cancellationToken)
        {
            return (await Set.FirstOrDefaultAsync(x => x.ObjectId == objectId, cancellationToken) != default(TStoredModel));
        }

        public async Task<TStoredModel> Create(TStoredModel model, CancellationToken cancellationToken)
        {
            Set.Add(model);
            await dbContext.SaveChangesAsync(cancellationToken);
            return await Set.FirstOrDefaultAsync(x => x.ObjectId == model.ObjectId, cancellationToken);
        }

        public async Task Delete(Guid objectId, CancellationToken cancellationToken)
        {
            var entity = await Set.FirstOrDefaultAsync(x => x.ObjectId == objectId, cancellationToken);
            if (entity != null)
            {
                Set.Remove(entity);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task<ICollection<TStoredModel>> Get(CancellationToken cancellationToken)
        {
            return await Set.ToListAsync(cancellationToken);
        }

        public async Task<ICollection<TStoredModel>> Get(Func<TStoredModel, bool> predicate, CancellationToken cancellationToken)
        {
            return await Task.FromResult(Set.Where(predicate).ToList());
        }

        public Task<TStoredModel> GetOne(Guid objectId, CancellationToken cancellationToken)
        {
            return Set.FirstOrDefaultAsync(x => x.ObjectId == objectId, cancellationToken);
        }

        public Task<TStoredModel> GetOne(Func<TStoredModel, bool> predicate, CancellationToken cancellationToken)
        {
            return Task.FromResult(Set.Where(predicate).FirstOrDefault());
        }

        public async Task<TStoredModel> Update(TStoredModel model, CancellationToken cancellationToken)
        {
            var entity = await Set.FirstOrDefaultAsync(x => x.ObjectId == model.ObjectId, cancellationToken);
            if (entity != null)
            {
                Set.Remove(entity);
                Set.Add(model);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            return await Set.FirstOrDefaultAsync(x => x.ObjectId == model.ObjectId, cancellationToken);
        }
    }
}
