// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers;
using AuthJanitor.Repository.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AuthJanitor.Repository
{
    public static class Extensions
    {
        public static void TryInitializeDatabase(this IServiceProvider app)
        {
            var serviceScopeFactory = app.GetRequiredService<IServiceScopeFactory>();
            using (var serviceScope = serviceScopeFactory.CreateScope())
            {
                var dbContext = serviceScope.ServiceProvider.GetService<AuthJanitorDbContext>();
                dbContext.Database.EnsureCreated();
            }
        }

        public static async Task<ResourceModel> CreateFromSuggestion(
            this DbSet<ResourceModel> resources,
            ProviderResourceSuggestion suggestion) =>
            (await resources.AddAsync(new ResourceModel()
            {
                DescriptiveName = suggestion.Name,
                Parameters = suggestion.Parameters,
            })).Entity;

        public static async Task<ResourceModel> CreateFromParameters(
            this DbSet<ResourceModel> resources,
            ProviderExecutionParameters executionParameters,
            string name = "New Resource") =>
            (await resources.AddAsync(new ResourceModel()
            {
                DescriptiveName = name,
                Parameters = executionParameters,
            })).Entity;

        public static async Task<ResourceDependencyGroupModel> Create(
            this DbSet<ResourceDependencyGroupModel> resourcesDependencyGroups,
            DependencyGroupModel resourceGroup,
            ResourceModel resource) =>
            (await resourcesDependencyGroups.AddAsync(new ResourceDependencyGroupModel()
            {
                Resource = resource,
                DependencyGroup = resourceGroup
            })).Entity;

        private static AuthJanitorDbContext GetContext<T>(
            this DbSet<T> dbSet) where T : class =>
            dbSet.GetService<ICurrentDbContext>().Context as AuthJanitorDbContext;

        public static async Task<DependencyGroupModel> CreateFromSuggestion(
            this DbSet<DependencyGroupModel> dependencyGroups,
            ProviderResourceSuggestion suggestion,
            string name)
        {
            var resources =
                (await Task.WhenAll(suggestion.ResourcesAddressingThis.Select(
                    async r => await dependencyGroups.GetContext()
                                .Resources.CreateFromSuggestion(r))))
                .ToList();
            resources.Add(await dependencyGroups.GetContext()
                     .Resources.CreateFromSuggestion(suggestion));

            var resourceGroup = new DependencyGroupModel()
            {
                Name = name
            };
            await dependencyGroups.AddAsync(resourceGroup);

            var membership = await Task.WhenAll(resources.Select(async r =>
                await dependencyGroups.GetContext().ResourceDependencyGroups.Create(
                    resourceGroup, r)));
            resourceGroup.Resources = membership;

            return resourceGroup;
        }

        public static async Task<DependencyGroupRotationModel> CreateNewTask(
            this DbSet<DependencyGroupRotationModel> rotations,
            DependencyGroupModel dependencyGroup,
            DateTimeOffset executionTime) =>
            (await rotations.AddAsync(new DependencyGroupRotationModel()
            {
                DependencyGroup = dependencyGroup,
                ExecutionTime = executionTime,
                HasExecuted = false,
                
            })).Entity;
    }
}
