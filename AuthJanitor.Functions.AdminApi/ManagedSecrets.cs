// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.UI.Shared.MetaServices;
using AuthJanitor.UI.Shared.Models;
using AuthJanitor.UI.Shared.ViewModels;
using AuthJanitor.CryptographicImplementations;
using AuthJanitor.EventSinks;
using AuthJanitor.IdentityServices;
using AuthJanitor.Integrations.DataStores;
using AuthJanitor.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AuthJanitor
{
    /// <summary>
    /// API functions to control the creation and management of AuthJanitor Managed Secrets.
    /// A Managed Secret is a grouping of Resources and Policies which describe the strategy around rekeying an object and the applications which consume it.
    /// </summary>
    public class ManagedSecrets
    {
        private readonly AuthJanitorCoreConfiguration _configuration;
        private readonly IIdentityService _identityService;
        private readonly ICryptographicImplementation _cryptographicImplementation;
        private readonly ProviderManagerService _providerManager;
        private readonly EventDispatcherMetaService _eventDispatcher;

        private readonly IDataStore<ManagedSecret> _managedSecrets;
        private readonly IDataStore<Resource> _resources;
        private readonly Func<ManagedSecret, ManagedSecretViewModel> _managedSecretViewModel;

        public ManagedSecrets(
            IOptions<AuthJanitorCoreConfiguration> configuration,
            IIdentityService identityService,
            ICryptographicImplementation cryptographicImplementation,
            EventDispatcherMetaService eventDispatcher,
            ProviderManagerService providerManager,
            IDataStore<ManagedSecret> managedSecretStore,
            IDataStore<Resource> resourceStore,
            Func<ManagedSecret, ManagedSecretViewModel> managedSecretViewModelDelegate)
        {
            _configuration = configuration.Value;
            _identityService = identityService;
            _cryptographicImplementation = cryptographicImplementation;
            _eventDispatcher = eventDispatcher;
            _providerManager = providerManager;

            _managedSecrets = managedSecretStore;
            _resources = resourceStore;
            _managedSecretViewModel = managedSecretViewModelDelegate;
        }

        [FunctionName("ManagedSecrets-Create")]
        public async Task<IActionResult> Create([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "managedSecrets")] ManagedSecretViewModel inputSecret)
        {
            if (!_identityService.CurrentUserHasRole(AuthJanitorRoles.SecretAdmin)) return new UnauthorizedResult();

            var resources = await _resources.Get();
            var resourceIds = inputSecret.ResourceIds.Split(';').Select(r => Guid.Parse(r)).ToList();
            if (resourceIds.Any(id => !resources.Any(r => r.ObjectId == id)))
            {
                await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.AnomalousEventOccurred, nameof(ManagedSecrets.Create), "New Managed Secret attempted to use one or more invalid Resource IDs");
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
                Nonce = await _cryptographicImplementation.GenerateCryptographicallySecureString(_configuration.DefaultNonceLength)
            };

            await _managedSecrets.Create(newManagedSecret);

            await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.SecretCreated, nameof(ManagedSecrets.Create), newManagedSecret);

            return new OkObjectResult(_managedSecretViewModel(newManagedSecret));
        }

        [FunctionName("ManagedSecrets-List")]
        public async Task<IActionResult> List([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "managedSecrets")] HttpRequest req)
        {
            _ = req;

            if (!_identityService.IsUserLoggedIn) return new UnauthorizedResult();

            return new OkObjectResult((await _managedSecrets.Get()).Select(s => _managedSecretViewModel(s)));
        }

        [FunctionName("ManagedSecrets-Get")]
        public async Task<IActionResult> Get([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "managedSecrets/{secretId:guid}")] HttpRequest req,
            Guid secretId)
        {
            _ = req;

            if (!_identityService.IsUserLoggedIn) return new UnauthorizedResult();

            if (!await _managedSecrets.ContainsId(secretId))
            {
                await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.AnomalousEventOccurred, nameof(ManagedSecrets.Get), "Secret ID not found");
                return new NotFoundObjectResult("Secret not found!");
            }

            return new OkObjectResult(_managedSecretViewModel(await _managedSecrets.GetOne(secretId)));
        }

        [FunctionName("ManagedSecrets-Delete")]
        public async Task<IActionResult> Delete([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "managedSecrets/{secretId:guid}")] HttpRequest req,
            Guid secretId)
        {
            _ = req;

            if (!_identityService.CurrentUserHasRole(AuthJanitorRoles.SecretAdmin)) return new UnauthorizedResult();

            if (!await _managedSecrets.ContainsId(secretId))
            {
                await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.AnomalousEventOccurred, nameof(ManagedSecrets.Delete), "Secret ID not found");
                return new NotFoundObjectResult("Secret not found!");
            }

            await _managedSecrets.Delete(secretId);

            await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.SecretDeleted, nameof(ManagedSecrets.Delete), secretId);

            return new OkResult();
        }

        [FunctionName("ManagedSecrets-Update")]
        public async Task<IActionResult> Update(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "managedSecrets/{secretId:guid}")] ManagedSecretViewModel inputSecret,
            Guid secretId)
        {
            if (!_identityService.CurrentUserHasRole(AuthJanitorRoles.SecretAdmin)) return new UnauthorizedResult();

            if (!await _managedSecrets.ContainsId(secretId))
            {
                await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.AnomalousEventOccurred, nameof(ManagedSecrets.Update), "Secret ID not found");
                return new NotFoundObjectResult("Secret not found!");
            }

            var resources = await _resources.Get();
            var resourceIds = inputSecret.ResourceIds.Split(';').Select(r => Guid.Parse(r)).ToList();
            if (resourceIds.Any(id => !resources.Any(r => r.ObjectId == id)))
            {
                await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.AnomalousEventOccurred, nameof(ManagedSecrets.Update), "New Managed Secret attempted to use one or more invalid Resource IDs");
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
                Nonce = await _cryptographicImplementation.GenerateCryptographicallySecureString(_configuration.DefaultNonceLength)
            };

            await _managedSecrets.Update(newManagedSecret);

            await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.SecretUpdated, nameof(ManagedSecrets.Update), newManagedSecret);

            return new OkObjectResult(_managedSecretViewModel(newManagedSecret));
        }
    }
}
