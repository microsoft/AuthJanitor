// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AuthJanitor.Functions
{
    public class System
    {
        private readonly SystemService _service;

        public System(SystemService service)
        {
            _service = service;
        }

        [FunctionName("System-GetIntegrityReports")]
        public async Task<IActionResult> GetIntegrityReports(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "system/integrityReports")] HttpRequest req, 
            CancellationToken cancellationToken)
        {
            return await _service.GetIntegrityReports(req, cancellationToken);
        }
    }
}
