// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.EventSinks;
using AuthJanitor.IdentityServices;
using AuthJanitor.Integrations.DataStores;
using AuthJanitor.Providers;
using AuthJanitor.UI.Shared;
using AuthJanitor.UI.Shared.MetaServices;
using AuthJanitor.UI.Shared.Models;
using AuthJanitor.UI.Shared.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;

namespace AuthJanitor.Services
{
    /// <summary>
    /// API functions to control the creation management, and approval of Rekeying Tasks.
    /// A Rekeying Task is a time-bounded description of one or more Managed Secrets to be rekeyed.
    /// </summary>
    public class RekeyingTasksService
    {
        private readonly AuthJanitorCoreConfiguration _configuration;
        private readonly IIdentityService _identityService;
        private readonly TaskExecutionMetaService _taskExecutionMetaService;
        private readonly ProviderManagerService _providerManager;
        private readonly EventDispatcherMetaService _eventDispatcher;

        private readonly IDataStore<ManagedSecret> _managedSecrets;
        private readonly IDataStore<RekeyingTask> _rekeyingTasks;
        private readonly Func<RekeyingTask, RekeyingTaskViewModel> _rekeyingTaskViewModel;

        public RekeyingTasksService(
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

        public async Task<IActionResult> Create(HttpRequest req, Guid secretId)
        {
            _ = req;

            if (!_identityService.CurrentUserHasRole(AuthJanitorRoles.ServiceOperator)) return new UnauthorizedResult();

            if (!await _managedSecrets.ContainsId(secretId))
            {
                await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.AnomalousEventOccurred, nameof(RekeyingTasksService.Create), "Secret ID not found");
                return new NotFoundObjectResult("Secret not found!");
            }

            var secret = await _managedSecrets.GetOne(secretId);
            if (!secret.TaskConfirmationStrategies.HasFlag(TaskConfirmationStrategies.AdminCachesSignOff) &&
                !secret.TaskConfirmationStrategies.HasFlag(TaskConfirmationStrategies.AdminSignsOffJustInTime))
            {
                await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.AnomalousEventOccurred, nameof(RekeyingTasksService.Create), "Managed Secret does not support adminstrator approval");
                return new BadRequestErrorMessageResult("Managed Secret does not support administrator approval!");
            }

            RekeyingTask newTask = new RekeyingTask()
            {
                Queued = DateTimeOffset.UtcNow,
                Expiry = secret.Expiry,
                ManagedSecretId = secret.ObjectId
            };

            await _rekeyingTasks.Create(newTask);

            await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.RotationTaskCreatedForApproval, nameof(RekeyingTasksService.Create), newTask);

            return new OkObjectResult(newTask);
        }

        public async Task<IActionResult> List(HttpRequest req)
        {
            _ = req;

            if (!_identityService.IsUserLoggedIn) return new UnauthorizedResult();

            return new OkObjectResult((await _rekeyingTasks.Get()).Select(t => _rekeyingTaskViewModel(t)));
        }

        public async Task<IActionResult> Get(HttpRequest req, Guid taskId)
        {
            _ = req;

            if (!_identityService.IsUserLoggedIn) return new UnauthorizedResult();

            if (!await _rekeyingTasks.ContainsId(taskId))
            {
                await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.AnomalousEventOccurred, nameof(RekeyingTasksService.Get), "Rekeying Task not found");
                return new NotFoundResult();
            }

            return new OkObjectResult(_rekeyingTaskViewModel((await _rekeyingTasks.GetOne(taskId))));
        }

        public async Task<IActionResult> Delete(HttpRequest req, Guid taskId)
        {
            _ = req;

            if (!_identityService.CurrentUserHasRole(AuthJanitorRoles.ServiceOperator)) return new UnauthorizedResult();

            if (!await _rekeyingTasks.ContainsId(taskId))
            {
                await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.AnomalousEventOccurred, nameof(RekeyingTasksService.Delete), "Rekeying Task not found");
                return new NotFoundResult();
            }

            await _rekeyingTasks.Delete(taskId);

            await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.RotationTaskDeleted, nameof(RekeyingTasksService.Delete), taskId);

            return new OkResult();
        }

        public async Task<IActionResult> Approve(HttpRequest req, Guid taskId)
        {
            _ = req;

            if (!_identityService.CurrentUserHasRole(AuthJanitorRoles.ServiceOperator)) return new UnauthorizedResult();

            var toRekey = await _rekeyingTasks.GetOne(t => t.ObjectId == taskId);
            if (toRekey == null)
            {
                await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.AnomalousEventOccurred, nameof(RekeyingTasksService.Delete), "Rekeying Task not found");
                return new NotFoundResult();
            }
            if (!toRekey.ConfirmationType.UsesOBOTokens())
            {
                await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.AnomalousEventOccurred, nameof(RekeyingTasksService.Approve), "Rekeying Task does not support Administrator approval");
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
