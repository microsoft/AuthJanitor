// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers;
using AuthJanitor.Repository;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;

namespace AuthJanitor.Automation.Blazor.Controllers
{
    [Route("api/system")]
    [ApiController]
    public class SystemController : ControllerBase
    {
        private readonly AuthJanitorDbContext _context;
        private readonly AuthJanitorService _authJanitorService;

        public SystemController(
            AuthJanitorDbContext context,
            AuthJanitorService authJanitorService)
        {
            _context = context;
            _authJanitorService = authJanitorService;
        }

        [HttpGet("providers")]
        public ActionResult<IEnumerable<LoadedProviderMetadata>> EnumerateLoadedProviders()
        {
            return Ok(_authJanitorService.LoadedProviders
                .Select(p => new LoadedProviderMetadata()
                {
                    ProviderTypeName = p.ProviderTypeName,
                    OriginatingFile = p.OriginatingFile,
                    Details = p.Details
                }));
        }

        [HttpGet("providers/{providerType}")]
        public ActionResult<AuthJanitorProviderConfiguration> CreateBlankConfiguration(
            string providerType)
        {
            return Ok(_authJanitorService
                        .ProviderManager
                        .GetProviderConfiguration(providerType));
        }
    }
}
