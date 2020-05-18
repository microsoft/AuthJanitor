// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Automation.Shared.MetaServices;
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
    public class Providers
    {
        private readonly IIdentityService _identityService;
        private readonly EventDispatcherMetaService _eventDispatcher;
        private readonly ProviderManagerService _providerManager;

        private readonly Func<AuthJanitorProviderConfiguration, ProviderConfigurationViewModel> _configViewModel;
        private readonly Func<LoadedProviderMetadata, LoadedProviderViewModel> _providerViewModel;

        public Providers(
            IIdentityService identityService,
            EventDispatcherMetaService eventDispatcher,
            ProviderManagerService providerManager,
            Func<AuthJanitorProviderConfiguration, ProviderConfigurationViewModel> configViewModelDelegate,
            Func<LoadedProviderMetadata, LoadedProviderViewModel> providerViewModelDelegate)
        {
            _identityService = identityService;
            _eventDispatcher = eventDispatcher;
            _providerManager = providerManager;

            _configViewModel = configViewModelDelegate;
            _providerViewModel = providerViewModelDelegate;
        }

        [FunctionName("Providers-List")]
        public IActionResult List([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "providers")] HttpRequest req)
        {
            _ = req;

            if (!_identityService.IsUserLoggedIn) return new UnauthorizedResult();

            return new OkObjectResult(_providerManager.LoadedProviders.Select(p => _providerViewModel(p)));
        }

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
            return new OkObjectResult(_configViewModel(_providerManager.GetProviderConfiguration(provider.ProviderTypeName)));
        }
    }
}
