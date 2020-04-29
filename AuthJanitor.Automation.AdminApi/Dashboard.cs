// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Automation.Shared;
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
    public class Dashboard : StorageIntegratedFunction
    {
        private readonly CredentialProviderService _credentialProviderService;
        private readonly ProviderManagerService _providerManager;

        public Dashboard(
            CredentialProviderService credentialProviderService,
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
            _credentialProviderService = credentialProviderService;
            _providerManager = providerManager;
        }

        [ProtectedApiEndpoint]
        [FunctionName("Dashboard")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "dashboard")] HttpRequest req)
        {
            if (!req.IsValidUser()) return new UnauthorizedResult();

            var allSecrets = await ManagedSecrets.ListAsync();
            var allResources = await Resources.ListAsync();
            var allTasks = await RekeyingTasks.ListAsync();

            var expiringInNextWeek = allSecrets.Where(s => DateTimeOffset.UtcNow.AddDays(7) < (s.LastChanged + s.ValidPeriod));
            var expired = allSecrets.Where(s => !s.IsValid);

            var metrics = new DashboardMetricsViewModel()
            {
                SignedInName = _credentialProviderService.GetCurrentUserName(),
                SignedInEmail = _credentialProviderService.GetCurrentUserEmail(),
                SignedInRole = AuthJanitorRoleExtensions.GetUserRole(req),
                TotalResources = allResources.Count,
                TotalSecrets = allSecrets.Count,
                TotalPendingApproval = allTasks.Where(t =>
                    t.ConfirmationType.HasFlag(TaskConfirmationStrategies.AdminCachesSignOff) ||
                    t.ConfirmationType.HasFlag(TaskConfirmationStrategies.AdminSignsOffJustInTime)).Count(),
                TotalExpiringSoon = expiringInNextWeek.Count(),
                TotalExpired = expired.Count(),
                ExpiringSoon = expiringInNextWeek.Select(s => GetViewModel(s)),
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
