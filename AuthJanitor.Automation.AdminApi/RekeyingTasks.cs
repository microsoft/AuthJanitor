// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Automation.Shared;
using AuthJanitor.Automation.Shared.MetaServices;
using AuthJanitor.Automation.Shared.Models;
using AuthJanitor.Automation.Shared.ViewModels;
using AuthJanitor.Integrations;
using AuthJanitor.Integrations.DataStores;
using AuthJanitor.Integrations.EventSinks;
using AuthJanitor.Integrations.IdentityServices;
using AuthJanitor.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;

namespace AuthJanitor.Automation.AdminApi
{
    /// <summary>
    /// API functions to control the creation management, and approval of Rekeying Tasks.
    /// A Rekeying Task is a time-bounded description of one or more Managed Secrets to be rekeyed.
    /// </summary>
    public class RekeyingTasks
    {
        private readonly AuthJanitorCoreConfiguration _configuration;
        private readonly IIdentityService _identityService;
        private readonly TaskExecutionMetaService _taskExecutionMetaService;
        private readonly ProviderManagerService _providerManager;
        private readonly EventDispatcherMetaService _eventDispatcher;

        private readonly IDataStore<ManagedSecret> _managedSecrets;
        private readonly IDataStore<RekeyingTask> _rekeyingTasks;
        private readonly Func<RekeyingTask, RekeyingTaskViewModel> _rekeyingTaskViewModel;

        public RekeyingTasks(
            IOptions<AuthJanitorCoreConfiguration> configuration,
            IIdentityService identityService,
            TaskExecutionMetaService taskExecutionMetaService,
            EventDispatcherMetaService eventDispatcher,
            ProviderManagerService providerManager,
            IDataStore<ManagedSecret> managedSecretStore,
            IDataStore<RekeyingTask> rekeyingTaskStore,
            Func<RekeyingTask, RekeyingTaskViewModel> rekeyingTaskViewModelDelegate)
        {
            _configuration = configuration.Value;
            _identityService = identityService;
            _taskExecutionMetaService = taskExecutionMetaService;
            _eventDispatcher = eventDispatcher;
            _providerManager = providerManager;

            _managedSecrets = managedSecretStore;
            _rekeyingTasks = rekeyingTaskStore;
            _rekeyingTaskViewModel = rekeyingTaskViewModelDelegate;
        }

        [FunctionName("RekeyingTasks-Create")]
        public async Task<IActionResult> Create(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tasks/{secretId:guid}")] HttpRequest req,
            Guid secretId)
        {
            _ = req;

            if (!_identityService.CurrentUserHasRole(AuthJanitorRoles.ServiceOperator)) return new UnauthorizedResult();

            if (!await _managedSecrets.ContainsId(secretId))
            {
                await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.AnomalousEventOccurred, nameof(AdminApi.RekeyingTasks.Create), "Secret ID not found");
                return new NotFoundObjectResult("Secret not found!");
            }

            var secret = await _managedSecrets.GetOne(secretId);
            if (!secret.TaskConfirmationStrategies.HasFlag(TaskConfirmationStrategies.AdminCachesSignOff) &&
                !secret.TaskConfirmationStrategies.HasFlag(TaskConfirmationStrategies.AdminSignsOffJustInTime))
            {
                await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.AnomalousEventOccurred, nameof(AdminApi.RekeyingTasks.Create), "Managed Secret does not support adminstrator approval");
                return new BadRequestErrorMessageResult("Managed Secret does not support administrator approval!");
            }

            RekeyingTask newTask = new RekeyingTask()
            {
                Queued = DateTimeOffset.UtcNow,
                Expiry = secret.Expiry,
                ManagedSecretId = secret.ObjectId
            };

            await _rekeyingTasks.Create(newTask);

            await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.RotationTaskCreatedForApproval, nameof(AdminApi.RekeyingTasks.Create), newTask);

            return new OkObjectResult(newTask);
        }

        [FunctionName("RekeyingTasks-List")]
        public async Task<IActionResult> List([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tasks")] HttpRequest req)
        {
            _ = req;

            if (!_identityService.IsUserLoggedIn) return new UnauthorizedResult();

            return new OkObjectResult((await _rekeyingTasks.Get()).Select(t => _rekeyingTaskViewModel(t)));
        }

        [FunctionName("RekeyingTasks-Get")]
        public async Task<IActionResult> Get([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tasks/{taskId:guid}")] HttpRequest req,
            Guid taskId)
        {
            _ = req;

            if (!_identityService.IsUserLoggedIn) return new UnauthorizedResult();

            if (!await _rekeyingTasks.ContainsId(taskId))
            {
                await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.AnomalousEventOccurred, nameof(AdminApi.RekeyingTasks.Get), "Rekeying Task not found");
                return new NotFoundResult();
            }

            return new OkObjectResult(_rekeyingTaskViewModel((await _rekeyingTasks.GetOne(taskId))));
        }

        [FunctionName("RekeyingTasks-Delete")]
        public async Task<IActionResult> Delete([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "tasks/{taskId:guid}")] HttpRequest req,
            Guid taskId)
        {
            _ = req;

            if (!_identityService.CurrentUserHasRole(AuthJanitorRoles.ServiceOperator)) return new UnauthorizedResult();

            if (!await _rekeyingTasks.ContainsId(taskId))
            {
                await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.AnomalousEventOccurred, nameof(AdminApi.RekeyingTasks.Delete), "Rekeying Task not found");
                return new NotFoundResult();
            }

            await _rekeyingTasks.Delete(taskId);

            await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.RotationTaskDeleted, nameof(AdminApi.RekeyingTasks.Delete), taskId);

            return new OkResult();
        }

        [FunctionName("RekeyingTasks-Approve")]
        public async Task<IActionResult> Approve([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tasks/{taskId:guid}/approve")] HttpRequest req,
            Guid taskId)
        {
            _ = req;

            if (!_identityService.CurrentUserHasRole(AuthJanitorRoles.ServiceOperator)) return new UnauthorizedResult();

            var toRekey = await _rekeyingTasks.GetOne(t => t.ObjectId == taskId);
            if (toRekey == null)
            {
                await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.AnomalousEventOccurred, nameof(AdminApi.RekeyingTasks.Delete), "Rekeying Task not found");
                return new NotFoundResult();
            }
            if (!toRekey.ConfirmationType.UsesOBOTokens())
            {
                await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.AnomalousEventOccurred, nameof(AdminApi.RekeyingTasks.Approve), "Rekeying Task does not support Administrator approval");
                return new BadRequestErrorMessageResult("Task does not support Administrator approval");
            }

            // Just cache credentials if no workflow action is required
            if (toRekey.ConfirmationType == TaskConfirmationStrategies.AdminCachesSignOff)
                await _taskExecutionMetaService.CacheBackCredentialsForTaskIdAsync(toRekey.ObjectId);
            else
                await _taskExecutionMetaService.ExecuteTask(toRekey.ObjectId);

            return new OkResult();
        }
    }
}
