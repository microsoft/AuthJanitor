// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.IdentityServices;
using AuthJanitor.Integrity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AuthJanitor.Services
{
    public class SystemService
    {
        private readonly IIdentityService _identityService;
        private readonly SystemIntegrityService _systemIntegrityService;

        public SystemService(
            IIdentityService identityService,
            SystemIntegrityService systemIntegrityService)
        {
            _identityService = identityService;
            _systemIntegrityService = systemIntegrityService;
        }

        public async Task<IActionResult> GetIntegrityReports(HttpRequest req, CancellationToken cancellationToken)
        {
            _ = req;

            if (!_identityService.IsUserLoggedIn) return new UnauthorizedResult();

            try
            {
                var reports = (await _systemIntegrityService.GetIntegrityReports())
                    .Where(r => r.IsAuthJanitorExtensionLibrary || r.IsAuthJanitorNamedLibrary);
                return new OkObjectResult(reports);
            }
            catch (Exception)
            {
                return new BadRequestResult();
            }
        }
    }
}
