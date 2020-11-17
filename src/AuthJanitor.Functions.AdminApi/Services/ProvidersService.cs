// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.UI.Shared;
using AuthJanitor.UI.Shared.MetaServices;
using AuthJanitor.UI.Shared.ViewModels;
using AuthJanitor.EventSinks;
using AuthJanitor.IdentityServices;
using AuthJanitor.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using AuthJanitor.Providers.Capabilities;
using AuthJanitor.ViewModels;
using Microsoft.Extensions.Logging;

namespace AuthJanitor.Services
{
    /// <summary>
    /// API functions to describe the loaded Providers and their configurations.
    /// A Provider is a library containing logic to either rekey an object/service or manage the lifecycle of an application.
    /// </summary>
    public class ProvidersService
    {
        private readonly IIdentityService _identityService;
        private readonly ILogger _logger;
        private readonly EventDispatcherMetaService _eventDispatcher;
        private readonly ProviderManagerService _providerManager;

        private readonly Func<AuthJanitorProviderConfiguration, ProviderConfigurationViewModel> _configViewModel;
        private readonly Func<LoadedProviderMetadata, LoadedProviderViewModel> _providerViewModel;

        public ProvidersService(
            IIdentityService identityService,
            ILogger<ProvidersService> logger,
            EventDispatcherMetaService eventDispatcher,
            ProviderManagerService providerManager,
            Func<AuthJanitorProviderConfiguration, ProviderConfigurationViewModel> configViewModelDelegate,
            Func<LoadedProviderMetadata, LoadedProviderViewModel> providerViewModelDelegate)
        {
            _identityService = identityService;
            _logger = logger;
            _eventDispatcher = eventDispatcher;
            _providerManager = providerManager;

            _configViewModel = configViewModelDelegate;
            _providerViewModel = providerViewModelDelegate;
        }

        public IActionResult List(HttpRequest req)
        {
            _ = req;

            if (!_identityService.IsUserLoggedIn) return new UnauthorizedResult();

            return new OkObjectResult(_providerManager.LoadedProviders.Select(p => _providerViewModel(p)));
        }

        public async Task<IActionResult> Enumerate(HttpRequest req)
        {
            _ = req;

            if (!_identityService.IsUserLoggedIn) return new UnauthorizedResult();

            _logger.LogInformation("Acquiring OBO token");
            var token = await _identityService.GetAccessTokenOnBehalfOfCurrentUserAsync();

            _logger.LogInformation("Starting provider enumeration");
            var providerSuggestions = await _providerManager.EnumerateProviders(token);

            _logger.LogInformation("Enumerated {Count} provider suggestions", providerSuggestions.Count());

            var results = new System.Collections.Generic.List<ProviderResourceSuggestionViewModel>();
            return new OkObjectResult(providerSuggestions.Select(p =>
            {
                try
                {
                    return GetSuggestionViewModel(p);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error preparing suggestion: " + ex.ToString());
                    return null;
                }
            }).Where(s => s != null));
        }
        private ProviderResourceSuggestionViewModel GetSuggestionViewModel(ProviderResourceSuggestion p) =>
            new ProviderResourceSuggestionViewModel()
            {
                Name = p.Name,
                ProviderType = p.ProviderType,
                ProviderConfiguration = _configViewModel(p.Configuration),
                ProviderConfigurationSerialized = p.SerializedConfiguration,
                ResourcesAddressingThis = p.ResourcesAddressingThis.Select(r => GetSuggestionViewModel(r))
            };

        public async Task<IActionResult> GetBlankConfiguration(HttpRequest req, string providerType)
        {
            _ = req;

            if (!_identityService.IsUserLoggedIn) return new UnauthorizedResult();

            var provider = _providerManager.LoadedProviders.FirstOrDefault(p => p.ProviderTypeName == providerType);
            if (provider == null)
            {
                await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.AnomalousEventOccurred, nameof(ProvidersService.GetBlankConfiguration), "Invalid Provider specified");
                return new NotFoundResult();
            }
            return new OkObjectResult(_configViewModel(_providerManager.GetProviderConfiguration(provider.ProviderTypeName)));
        }

        public async Task<IActionResult> TestConfiguration(
            string providerConfiguration,
            HttpRequest req,
            string providerType,
            string testContext)
        {
            _ = req;

            if (!_identityService.IsUserLoggedIn) return new UnauthorizedResult();

            Enum.TryParse<TestAsContexts>(testContext, true, out TestAsContexts testContextEnum);
            AccessTokenCredential credential = null;
            try
            {
                switch (testContextEnum)
                {
                    case TestAsContexts.AsApp:
                        credential = await _identityService.GetAccessTokenForApplicationAsync();
                        break;
                    case TestAsContexts.AsUser:
                        credential = await _identityService.GetAccessTokenOnBehalfOfCurrentUserAsync();
                        break;
                    default:
                        return new BadRequestErrorMessageResult("Invalid test context");
                }
                if (credential == null || string.IsNullOrEmpty(credential.AccessToken))
                    throw new Exception("Credential was empty!");
            }
            catch (Exception ex)
            {
                return new BadRequestErrorMessageResult(
                    "Error retrieving Access Token: " + Environment.NewLine +
                    ex.Message + Environment.NewLine +
                    ex.StackTrace);
            }

            var provider = _providerManager.LoadedProviders.FirstOrDefault(p => p.ProviderTypeName == providerType);
            if (provider == null)
            {
                await _eventDispatcher.DispatchEvent(AuthJanitorSystemEvents.AnomalousEventOccurred, nameof(ProvidersService.GetBlankConfiguration), "Invalid Provider specified");
                return new NotFoundResult();
            }

            if (typeof(ICanRunSanityTests).IsAssignableFrom(provider.ProviderType))
            {
                try
                {
                    var instance = _providerManager.GetProviderInstance(provider.ProviderTypeName, providerConfiguration);
                    if (instance == null)
                        return new BadRequestErrorMessageResult("Provider configuration is invalid!");
                    instance.Credential = credential;
                    await (instance as ICanRunSanityTests).Test();
                }
                catch (Exception ex)
                {
                    return new BadRequestErrorMessageResult(ex.Message);
                }
            }
            else
                return new BadRequestErrorMessageResult("Provider does not support testing!");

            return new OkResult();
        }
    }
}
