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
    /// <summary>
    /// API functions to describe the loaded Providers and their configurations.
    /// A Provider is a library containing logic to either rekey an object/service or manage the lifecycle of an application.
    /// </summary>
    public class Providers
    {
        private readonly ProvidersService _service;

        public Providers(ProvidersService service)
        {
            _service = service;
        }

        [FunctionName("Providers-List")]
        public IActionResult List([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "providers")] HttpRequest req)
        {
            return _service.List(req);
        }

        [FunctionName("Providers-GetBlankConfiguration")]
        public async Task<IActionResult> GetBlankConfiguration(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "providers/{providerType}")] HttpRequest req,
            string providerType)
        {
            return await _service.GetBlankConfiguration(req, providerType);
        }

        [FunctionName("Providers-TestConfiguration")]
        public async Task<IActionResult> TestConfiguration(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "providers/{providerType}/test/{testContext}")] string providerConfiguration,
            HttpRequest req,
            string providerType,
            string testContext)
        {
            return await _service.TestConfiguration(providerConfiguration, req, providerType, testContext);
        }
    }
}
