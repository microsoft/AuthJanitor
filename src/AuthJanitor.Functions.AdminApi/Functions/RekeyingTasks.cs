// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AuthJanitor.Functions
{
    /// <summary>
    /// API functions to control the creation management, and approval of Rekeying Tasks.
    /// A Rekeying Task is a time-bounded description of one or more Managed Secrets to be rekeyed.
    /// </summary>
    public class RekeyingTasks
    {
        private readonly RekeyingTasksService _service;

        public RekeyingTasks(RekeyingTasksService service)
        {
            _service = service;
        }

        [FunctionName("RekeyingTasks-Create")]
        public async Task<IActionResult> Create(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tasks/{secretId:guid}")] HttpRequest req,
            Guid secretId, CancellationToken cancellationToken)
        {
            return await _service.Create(req, secretId, cancellationToken);
        }

        [FunctionName("RekeyingTasks-List")]
        public async Task<IActionResult> List([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tasks")] HttpRequest req, CancellationToken cancellationToken)
        {
            return await _service.List(req, cancellationToken);
        }

        [FunctionName("RekeyingTasks-Get")]
        public async Task<IActionResult> Get([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tasks/{taskId:guid}")] HttpRequest req,
            Guid taskId, CancellationToken cancellationToken)
        {
            return await _service.Get(req, taskId, cancellationToken);
        }

        [FunctionName("RekeyingTasks-Delete")]
        public async Task<IActionResult> Delete([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "tasks/{taskId:guid}")] HttpRequest req,
            Guid taskId, CancellationToken cancellationToken)
        {
            return await _service.Delete(req, taskId, cancellationToken);
        }

        [FunctionName("RekeyingTasks-Approve")]
        public async Task<IActionResult> Approve([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tasks/{taskId:guid}/approve")] HttpRequest req,
            Guid taskId, CancellationToken cancellationToken)
        {
            return await _service.Approve(req, taskId, cancellationToken);
        }
    }
}
