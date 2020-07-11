// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.EventSinks;
using AuthJanitor.IdentityServices;
using AuthJanitor.Integrations.DataStores;
using AuthJanitor.Providers;
using AuthJanitor.UI.Shared.MetaServices;
using AuthJanitor.UI.Shared.Models;
using AuthJanitor.UI.Shared.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace AuthJanitor.Services
{
    /// <summary>
    /// API functions to control the creation and management of AuthJanitor Resources.
    /// A Resource is the description of how to connect to an object or resource, using a given Provider.
    /// </summary>
    public class ResourcesService
    {
        private readonly IIdentityService _identityService;
        private readonly ProviderManagerService _providerManager;
        private readonly EventDispatcherMetaService _eventDispatcher;

        private readonly IDataStore<Resource> _resources;
        private readonly Func<Resource, ResourceViewModel> _resourceViewModel;

        public ResourcesService(
            IIdentityService identityService,
            EventDispatcherMetaService eventDispatcher,
            ProviderManagerService providerManager,
            IDataStore<Resource> resourceStore,
            Func<Resource, ResourceViewModel> resourceViewModelDelegate)
        {
            _identityService = identityService;
            _eventDispatcher = eventDispatcher;
            _providerManager = providerManager;

            _resources = resourceStore;
            _resourceViewModel = resourceViewModelDelegate;
        }

        public async Task<IActionResult> Create(ResourceViewModel resource, HttpRequest req, CancellationToken cancellationToken)
        {
            _ = req;

            if (!_identityService.CurrentUserHasRole(AuthJanitorRoles.ResourceAdmin)) return new UnauthorizedResult();

            var provider = _providerManager.GetProviderMetadata(resource.ProviderType);
            if (provider == null)
                return new NotFoundObjectResult("Provider type not found");

            if (!_providerManager.TestProviderConfiguration(provider.ProviderTypeName, resource.SerializedProviderConfiguration))
                return new BadRequestErrorMessageResult("Invalid Provider configuration!");

            Resource newResource = new Resource()
            {
                Name = resource.Name,
                Description = resource.Description,
                ProviderType = provider.ProviderTypeName,
                ProviderConfiguration = resource.SerializedProviderConfiguration
            };

            await _resources.Create(newResource, cancellationToken);

            await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.ResourceCreated, nameof(ResourcesService.Create), newResource);

            return new OkObjectResult(_resourceViewModel(newResource));
        }

        public async Task<IActionResult> List(HttpRequest req, CancellationToken cancellationToken)
        {
            _ = req;

            if (!_identityService.IsUserLoggedIn) return new UnauthorizedResult();

            return new OkObjectResult((await _resources.Get(cancellationToken)).Select(r => _resourceViewModel(r)));
        }

        public async Task<IActionResult> Get(HttpRequest req, Guid resourceId, CancellationToken cancellationToken)
        {
            _ = req;

            if (!_identityService.IsUserLoggedIn) return new UnauthorizedResult();

            if (!await _resources.ContainsId(resourceId, cancellationToken))
            {
                await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.AnomalousEventOccurred, nameof(ResourcesService.Get), "Resource not found");
                return new NotFoundResult();
            }

            return new OkObjectResult(_resourceViewModel(await _resources.GetOne(resourceId, cancellationToken)));
        }

        public async Task<IActionResult> Delete(HttpRequest req, Guid resourceId, CancellationToken cancellationToken)
        {
            _ = req;

            if (!_identityService.CurrentUserHasRole(AuthJanitorRoles.ResourceAdmin)) return new UnauthorizedResult();

            if (!await _resources.ContainsId(resourceId, cancellationToken))
            {
                await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.AnomalousEventOccurred, nameof(ResourcesService.Delete), "Resource not found");
                return new NotFoundResult();
            }

            await _resources.Delete(resourceId, cancellationToken);

            await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.ResourceDeleted, nameof(ResourcesService.Delete), resourceId);

            return new OkResult();
        }

        public async Task<IActionResult> Update(ResourceViewModel resource, HttpRequest req, Guid resourceId, CancellationToken cancellationToken)
        {
            _ = req;

            if (!_identityService.CurrentUserHasRole(AuthJanitorRoles.ResourceAdmin)) return new UnauthorizedResult();

            var provider = _providerManager.GetProviderMetadata(resource.ProviderType);
            if (provider == null)
                return new NotFoundObjectResult("Provider type not found");

            if (!_providerManager.TestProviderConfiguration(provider.ProviderTypeName, resource.SerializedProviderConfiguration))
                return new BadRequestErrorMessageResult("Invalid Provider configuration!");

            Resource newResource = new Resource()
            {
                ObjectId = resourceId,
                Name = resource.Name,
                Description = resource.Description,
                ProviderType = resource.ProviderType,
                ProviderConfiguration = resource.SerializedProviderConfiguration
            };

            await _resources.Update(newResource, cancellationToken);

            await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.ResourceUpdated, nameof(ResourcesService.Update), newResource);

            return new OkObjectResult(_resourceViewModel(newResource));
        }
    }
}
