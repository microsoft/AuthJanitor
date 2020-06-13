// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System.Threading.Tasks;

namespace AuthJanitor.Functions
{
    public class Dashboard
    {
        private readonly DashboardService _service;

        public Dashboard(DashboardService service)
        {
            _service = service;
        }

        [FunctionName("Dashboard")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "dashboard")] HttpRequest req)
        {
            return await _service.Run(req);
        }
    }
}
