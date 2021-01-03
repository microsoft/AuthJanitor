using AuthJanitor.Repository;
using AuthJanitor.Repository.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;

namespace AuthJanitor.Automation.Blazor.Controllers
{
    [Route("api/rotations")]
    [ApiController]
    public class DependencyGroupRotationController : ReadOnlyEntityControllerBase<DependencyGroupModel>
    {
        public DependencyGroupRotationController(AuthJanitorDbContext context) : base(context)
        {
        }

        protected override DbSet<DependencyGroupModel> DbSet => _context.DependencyGroups;

        protected override Func<DependencyGroupModel, Guid> GetId =>
            new Func<DependencyGroupModel, Guid>((r) => r.DependencyGroupId);
    }
}
