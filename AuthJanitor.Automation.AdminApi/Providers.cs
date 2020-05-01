// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Automation.Shared;
using AuthJanitor.Automation.Shared.MetaServices;
using AuthJanitor.Automation.Shared.Models;
using AuthJanitor.Automation.Shared.ViewModels;
using AuthJanitor.Integrations.EventSinks;
using AuthJanitor.Integrations.IdentityServices;
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
    /// API functions to describe the loaded Providers and their configurations.
    /// A Provider is a library containing logic to either rekey an object/service or manage the lifecycle of an application.
    /// </summary>
    public class Providers : StorageIntegratedFunction
    {
        private readonly IIdentityService _identityService;
        private readonly EventDispatcherMetaService _eventDispatcher;
        private readonly ProviderManagerService _providerManager;

        public Providers(
            IIdentityService identityService,
            EventDispatcherMetaService eventDispatcher,
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
            _identityService = identityService;
            _eventDispatcher = eventDispatcher;
            _providerManager = providerManager;
        }

        [ProtectedApiEndpoint]
        [FunctionName("Providers-List")]
        public IActionResult List([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "providers")] HttpRequest req)
        {
            _ = req;

            if (!_identityService.IsUserLoggedIn) return new UnauthorizedResult();

            return new OkObjectResult(_providerManager.LoadedProviders.Select(p => GetViewModel(p)));
        }

        [ProtectedApiEndpoint]
        [FunctionName("Providers-GetBlankConfiguration")]
        public async Task<IActionResult> GetBlankConfiguration(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "providers/{providerType}")] HttpRequest req,
            string providerType)
        {
            _ = req;

            if (!_identityService.IsUserLoggedIn) return new UnauthorizedResult();

            var provider = _providerManager.LoadedProviders.FirstOrDefault(p => p.ProviderTypeName == providerType);
            if (provider == null)
            {
                await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.AnomalousEventOccurred, nameof(AdminApi.Providers.GetBlankConfiguration), "Invalid Provider specified");
                return new NotFoundResult();
            }
            return new OkObjectResult(GetViewModel(_providerManager.GetProviderConfiguration(provider.ProviderTypeName)));
        }
    }
}
