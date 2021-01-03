using AuthJanitor.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace AuthJanitor.Automation.Blazor.Controllers
{
    public abstract class ReadWriteEntityControllerBase<TEntity> :
        ReadOnlyEntityControllerBase<TEntity>
        where TEntity : class
    {
        public ReadWriteEntityControllerBase(AuthJanitorDbContext context)
            : base(context) { }

        [HttpPost]
        public virtual async Task<ActionResult<TEntity>> Post(TEntity model)
        {
            DbSet.Add(model);
            await _context.SaveChangesAsync();

            return CreatedAtAction("Post", new { id = GetId(model) }, model);
        }

        [HttpDelete("{id}")]
        public virtual async Task<ActionResult<TEntity>> Delete(Guid id)
        {
            var model = await DbSet.FindAsync(id);
            if (model == null)
                return NotFound();

            DbSet.Remove(model);
            await _context.SaveChangesAsync();

            return model;
        }
    }
}
