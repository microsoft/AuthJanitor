// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.IdentityServices;
using AuthJanitor.Integrations.DataStores;
using AuthJanitor.Providers;
using AuthJanitor.UI.Shared;
using AuthJanitor.UI.Shared.Models;
using AuthJanitor.UI.Shared.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AuthJanitor.Services
{
    public class DashboardService
    {
        private readonly IDataStore<ManagedSecret> _managedSecrets;
        private readonly IDataStore<Resource> _resources;
        private readonly IDataStore<RekeyingTask> _rekeyingTasks;

        private readonly Func<ManagedSecret, ManagedSecretViewModel> _managedSecretViewModel;

        private readonly IIdentityService _identityService;
        private readonly ProviderManagerService _providerManager;

        public DashboardService(
            IIdentityService identityService,
            ProviderManagerService providerManager,
            IDataStore<ManagedSecret> managedSecretStore,
            IDataStore<Resource> resourceStore,
            IDataStore<RekeyingTask> rekeyingTaskStore,
            Func<ManagedSecret, ManagedSecretViewModel> managedSecretViewModelDelegate)
        {
            _identityService = identityService;
            _providerManager = providerManager;

            _managedSecrets = managedSecretStore;
            _resources = resourceStore;
            _rekeyingTasks = rekeyingTaskStore;

            _managedSecretViewModel = managedSecretViewModelDelegate;
        }

        public async Task<IActionResult> Run(HttpRequest req)
        {
            _ = req;

            if (!_identityService.IsUserLoggedIn) return new UnauthorizedResult();

            var allSecrets = await _managedSecrets.Get();
            var allResources = await _resources.Get();
            var allTasks = await _rekeyingTasks.Get();

            var expiringInNextWeek = allSecrets.Where(s => DateTimeOffset.UtcNow.AddDays(7) < (s.LastChanged + s.ValidPeriod));
            var expired = allSecrets.Where(s => !s.IsValid);

            var metrics = new DashboardMetricsViewModel()
            {
                SignedInName = _identityService.UserName,
                SignedInEmail = _identityService.UserEmail,
                SignedInRoles = string.Join(", ", _identityService.UserRoles),
                TotalResources = allResources.Count,
                TotalSecrets = allSecrets.Count,
                TotalPendingApproval = allTasks.Where(t =>
                    t.ConfirmationType.HasFlag(TaskConfirmationStrategies.AdminCachesSignOff) ||
                    t.ConfirmationType.HasFlag(TaskConfirmationStrategies.AdminSignsOffJustInTime)).Count(),
                TotalExpiringSoon = expiringInNextWeek.Count(),
                TotalExpired = expired.Count(),
                ExpiringSoon = expiringInNextWeek.Select(s => _managedSecretViewModel(s)),
                PercentExpired = (int)((double)expired.Count() / allSecrets.Count) * 100,
                TasksInError = allTasks.Count(t => t.RekeyingFailed)
            };

            foreach (var secret in allSecrets)
            {
                var riskScore = 0;
                foreach (var resourceId in secret.ResourceIds)
                {
                    var resource = allResources.FirstOrDefault(r => r.ObjectId == resourceId);

                    var provider = _providerManager.GetProviderInstance(
                        resource.ProviderType,
                        resource.ProviderConfiguration);
                    riskScore += provider.GetRisks(secret.ValidPeriod).Sum(r => r.Score);
                }
                if (riskScore > 85)
                    metrics.RiskOver85++;
                else if (riskScore > 60)
                    metrics.Risk85++;
                else if (riskScore > 35)
                    metrics.Risk60++;
                else if (riskScore > 0)
                    metrics.Risk35++;
                else if (riskScore == 0)
                    metrics.Risk0++;
            }

            return new OkObjectResult(metrics);
        }
    }
}
