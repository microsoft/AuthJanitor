using AuthJanitor.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthJanitor.Automation.Blazor.Controllers
{
    public abstract class ReadOnlyEntityControllerBase<TEntity> : ControllerBase
        where TEntity : class
    {
        protected readonly AuthJanitorDbContext _context;

        public ReadOnlyEntityControllerBase(AuthJanitorDbContext context)
        {
            _context = context;
        }

        protected abstract DbSet<TEntity> DbSet { get; }
        protected abstract Func<TEntity, Guid> GetId { get; }

        [HttpGet]
        public virtual async Task<ActionResult<IEnumerable<TEntity>>> Get() =>
            await DbSet.ToListAsync();

        [HttpGet("{id}")]
        public virtual async Task<ActionResult<TEntity>> Get(Guid id)
        {
            var model = await DbSet.FindAsync(id);
            if (model == null)
                return NotFound();
            else return model;
        }
    }
}
