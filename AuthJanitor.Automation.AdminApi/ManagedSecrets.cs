// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Automation.Shared;
using AuthJanitor.Automation.Shared.DataStores;
using AuthJanitor.Automation.Shared.Models;
using AuthJanitor.Automation.Shared.ViewModels;
using AuthJanitor.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AuthJanitor.Automation.AdminApi
{
    /// <summary>
    /// API functions to control the creation and management of AuthJanitor Managed Secrets.
    /// A Managed Secret is a grouping of Resources and Policies which describe the strategy around rekeying an object and the applications which consume it.
    /// </summary>
    public class ManagedSecrets : StorageIntegratedFunction
    {
        private readonly AuthJanitorServiceConfiguration _serviceConfiguration;
        private readonly ICryptographicImplementation _cryptographicImplementation;
        private readonly ProviderManagerService _providerManager;
        private readonly EventDispatcherService _eventDispatcher;

        public ManagedSecrets(
            AuthJanitorServiceConfiguration serviceConfiguration,
            ICryptographicImplementation cryptographicImplementation,
            EventDispatcherService eventDispatcher,
            ProviderManagerService providerManager,
            IDataStore<ManagedSecret> managedSecretStore,
            IDataStore<Resource> resourceStore,
            IDataStore<RekeyingTask> rekeyingTaskStore,
            Func<ManagedSecret, ManagedSecretViewModel> managedSecretViewModelDelegate,
            Func<Resource, ResourceViewModel> resourceViewModelDelegate,
            Func<RekeyingTask, RekeyingTaskViewModel> rekeyingTaskViewModelDelegate,
            Func<AuthJanitorProviderConfiguration, ProviderConfigurationViewModel> configViewModelDelegate,
            Func<ScheduleWindow, ScheduleWindowViewModel> scheduleViewModelDelegate,
            Func<LoadedProviderMetadata, LoadedProviderViewModel> providerViewModelDelegate) :
                base(managedSecretStore, resourceStore, rekeyingTaskStore, managedSecretViewModelDelegate, resourceViewModelDelegate, rekeyingTaskViewModelDelegate, configViewModelDelegate, scheduleViewModelDelegate, providerViewModelDelegate)
        {
            _serviceConfiguration = serviceConfiguration;
            _cryptographicImplementation = cryptographicImplementation;
            _eventDispatcher = eventDispatcher;
            _providerManager = providerManager;
        }

        [ProtectedApiEndpoint]
        [FunctionName("ManagedSecrets-Create")]
        public async Task<IActionResult> Create(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "managedSecrets")] ManagedSecretViewModel inputSecret,
            HttpRequest req)
        {
            if (!req.IsValidUser(AuthJanitorRoles.SecretAdmin, AuthJanitorRoles.GlobalAdmin)) return new UnauthorizedResult();

            var resources = await Resources.ListAsync();
            var resourceIds = inputSecret.ResourceIds.Split(';').Select(r => Guid.Parse(r)).ToList();
            if (resourceIds.Any(id => !resources.Any(r => r.ObjectId == id)))
            {
                await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.AnomalousEventOccurred, nameof(AdminApi.ManagedSecrets.Create), "New Managed Secret attempted to use one or more invalid Resource IDs");
                return new NotFoundObjectResult("One or more Resource IDs not found!");
            }

            ManagedSecret newManagedSecret = new ManagedSecret()
            {
                Name = inputSecret.Name,
                Description = inputSecret.Description,
                ValidPeriod = TimeSpan.FromMinutes(inputSecret.ValidPeriodMinutes),
                LastChanged = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(inputSecret.ValidPeriodMinutes),
                TaskConfirmationStrategies = inputSecret.TaskConfirmationStrategies,
                ResourceIds = resourceIds,
                Nonce = await _cryptographicImplementation.GenerateCryptographicallySecureString(_serviceConfiguration.DefaultNonceLength)
            };

            await ManagedSecrets.CreateAsync(newManagedSecret);

            await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.SecretCreated, nameof(AdminApi.ManagedSecrets.Create), newManagedSecret);

            return new OkObjectResult(GetViewModel(newManagedSecret));
        }

        [ProtectedApiEndpoint]
        [FunctionName("ManagedSecrets-List")]
        public async Task<IActionResult> List(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "managedSecrets")] HttpRequest req)
        {
            if (!req.IsValidUser()) return new UnauthorizedResult();

            return new OkObjectResult((await ManagedSecrets.ListAsync()).Select(s => GetViewModel(s)));
        }

        [ProtectedApiEndpoint]
        [FunctionName("ManagedSecrets-Get")]
        public async Task<IActionResult> Get(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "managedSecrets/{secretId:guid}")] HttpRequest req,
            Guid secretId)
        {
            if (!req.IsValidUser()) return new UnauthorizedResult();

            if (!await ManagedSecrets.ContainsIdAsync(secretId))
            {
                await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.AnomalousEventOccurred, nameof(AdminApi.ManagedSecrets.Get), "Secret ID not found");
                return new NotFoundObjectResult("Secret not found!");
            }

            return new OkObjectResult(GetViewModel(await ManagedSecrets.GetAsync(secretId)));
        }

        [ProtectedApiEndpoint]
        [FunctionName("ManagedSecrets-Delete")]
        public async Task<IActionResult> Delete(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "managedSecrets/{secretId:guid}")] HttpRequest req,
            Guid secretId)
        {
            if (!req.IsValidUser(AuthJanitorRoles.SecretAdmin, AuthJanitorRoles.GlobalAdmin)) return new UnauthorizedResult();

            if (!await ManagedSecrets.ContainsIdAsync(secretId))
            {
                await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.AnomalousEventOccurred, nameof(AdminApi.ManagedSecrets.Delete), "Secret ID not found");
                return new NotFoundObjectResult("Secret not found!");
            }

            await ManagedSecrets.DeleteAsync(secretId);

            await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.SecretDeleted, nameof(AdminApi.ManagedSecrets.Delete), secretId);

            return new OkResult();
        }

        [ProtectedApiEndpoint]
        [FunctionName("ManagedSecrets-Update")]
        public async Task<IActionResult> Update(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "managedSecrets/{secretId:guid}")] ManagedSecretViewModel inputSecret,
            HttpRequest req,
            Guid secretId)
        {
            if (!req.IsValidUser(AuthJanitorRoles.SecretAdmin, AuthJanitorRoles.GlobalAdmin)) return new UnauthorizedResult();

            if (!await ManagedSecrets.ContainsIdAsync(secretId))
            {
                await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.AnomalousEventOccurred, nameof(AdminApi.ManagedSecrets.Update), "Secret ID not found");
                return new NotFoundObjectResult("Secret not found!");
            }

            var resources = await Resources.ListAsync();
            var resourceIds = inputSecret.ResourceIds.Split(';').Select(r => Guid.Parse(r)).ToList();
            if (resourceIds.Any(id => !resources.Any(r => r.ObjectId == id)))
            {
                await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.AnomalousEventOccurred, nameof(AdminApi.ManagedSecrets.Update), "New Managed Secret attempted to use one or more invalid Resource IDs");
                return new NotFoundObjectResult("One or more Resource IDs not found!");
            }

            ManagedSecret newManagedSecret = new ManagedSecret()
            {
                ObjectId = secretId,
                Name = inputSecret.Name,
                Description = inputSecret.Description,
                ValidPeriod = TimeSpan.FromMinutes(inputSecret.ValidPeriodMinutes),
                TaskConfirmationStrategies = inputSecret.TaskConfirmationStrategies,
                ResourceIds = resourceIds,
                Nonce = await _cryptographicImplementation.GenerateCryptographicallySecureString(_serviceConfiguration.DefaultNonceLength)
            };

            await ManagedSecrets.UpdateAsync(newManagedSecret);

            await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.SecretUpdated, nameof(AdminApi.ManagedSecrets.Update), newManagedSecret);

            return new OkObjectResult(GetViewModel(newManagedSecret));
        }
    }
}
