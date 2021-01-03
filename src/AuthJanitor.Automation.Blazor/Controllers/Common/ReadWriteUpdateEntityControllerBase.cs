// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace AuthJanitor.Automation.Blazor.Controllers
{
    public abstract class ReadWriteUpdateEntityControllerBase<TEntity> :
        ReadWriteEntityControllerBase<TEntity>
        where TEntity : class
    {
        public ReadWriteUpdateEntityControllerBase(AuthJanitorDbContext context)
            : base(context) { }

        [HttpPut("{id}")]
        public virtual async Task<IActionResult> Put(Guid id, TEntity model)
        {
            if (id != GetId(model))
                return BadRequest();

            _context.Entry(model).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await DbSet.AnyAsync(r => GetId(r) == id))
                    return NotFound();
                throw;
            }

            return NoContent();
        }
    }
}
