using AuthJanitor.Providers;
using AuthJanitor.Repository;
using AuthJanitor.Repository.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthJanitor.Automation.Blazor.Controllers
{
    [Route("api/resources")]
    [ApiController]
    public class ResourceController : ReadWriteEntityControllerBase<ResourceModel>
    {
        private readonly AuthJanitorService _authJanitorService;
        private readonly TokenAbstractionService _tokens;

        public ResourceController(
            AuthJanitorDbContext context,
            AuthJanitorService authJanitorService,
            TokenAbstractionService tokenAbstraction) : base(context)
        {
            _authJanitorService = authJanitorService;
            _tokens = tokenAbstraction;
        }

        protected override DbSet<ResourceModel> DbSet => _context.Resources;

        protected override Func<ResourceModel, Guid> GetId =>
            new Func<ResourceModel, Guid>((r) => r.ResourceId);

        [HttpGet("enumerate")]
        public async Task<ActionResult<IEnumerable<ProviderResourceSuggestion>>> Enumerate()
        {
            var token = await _tokens.GetAzureAsUser();
            var resources = await _authJanitorService.EnumerateAsync(token);
            return Ok(
                JsonConvert.SerializeObject(resources,
                 new JsonSerializerSettings()
                 {
                     ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                 }));
        }
    }
}
