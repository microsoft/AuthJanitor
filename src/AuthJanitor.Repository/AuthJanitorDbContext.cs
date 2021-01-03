// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers;
using AuthJanitor.Repository.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Newtonsoft.Json;
using System;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

namespace AuthJanitor.Repository
{
    public class AuthJanitorDbContext : DbContext
    {
        public DbSet<ResourceModel> Resources { get; set; }
        public DbSet<DependencyGroupModel> DependencyGroups { get; set; }
        public DbSet<ResourceDependencyGroupModel> ResourceDependencyGroups { get; set; }
        public DbSet<DependencyGroupRotationModel> DependencyGroupRotations { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(CreateInMemoryDatabase());
        }

        private static DbConnection CreateInMemoryDatabase()
        {
            var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();
            return connection;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder
                .Entity<ResourceModel>()
                .Property(e => e.Parameters)
                .HasConversion(GetJsonConverter<ProviderExecutionParameters>());

            modelBuilder
                .Entity<DependencyGroupRotationModel>()
                .Property(e => e.WorkflowActions)
                .HasConversion(GetJsonConverter<ProviderWorkflowActionCollection>());

            modelBuilder
                .Entity<DependencyGroupModel>()
                .Property(e => e.DefaultParameters)
                .HasConversion(GetJsonConverter<ProviderExecutionParameters>());

            // *:* join table
            modelBuilder.Entity<ResourceDependencyGroupModel>()
                .HasKey(rdg => new { rdg.DependencyGroupId, rdg.ResourceId });
            modelBuilder.Entity<ResourceDependencyGroupModel>()
                .HasOne(rdg => rdg.Resource)
                .WithMany(r => r.DependencyGroups)
                .HasForeignKey(rdg => rdg.ResourceId);
            modelBuilder.Entity<ResourceDependencyGroupModel>()
                .HasOne(rdg => rdg.DependencyGroup)
                .WithMany(r => r.Resources)
                .HasForeignKey(rdg => rdg.DependencyGroupId);

            // 1:*
            modelBuilder.Entity<DependencyGroupRotationModel>()
                .HasOne(dgr => dgr.DependencyGroup)
                .WithMany(dg => dg.Rotations)
                .HasForeignKey(dgr => dgr.DependencyGroupId);

            modelBuilder.Entity<DependencyGroupRotationModel>()
                .HasIndex(dgr => dgr.DependencyGroupId);
            modelBuilder.Entity<DependencyGroupRotationModel>()
                .HasIndex(dgr => new { dgr.HasExecuted, dgr.ExecutionTime });
        }

        public async Task RunScheduledTasks(
            AuthJanitorService svc,
            Func<Task<AccessTokenCredential>> getOboCallback,
            Func<Task<AccessTokenCredential>> getMsiCallback)
        {
            // schedule new tasks based on things that have passed
            var targets = await DependencyGroups
                .Where(dg => dg.LastRotation.Add(dg.ValidPeriod) < DateTimeOffset.UtcNow)
                .Where(dg => !dg.Rotations.All(r => r.HasExecuted))
                .ToListAsync();
            
            await Task.WhenAll(targets.Select(item => DependencyGroupRotations.CreateNewTask(
                    item,
                    item.LastRotation.Add(item.ValidPeriod))));
            await SaveChangesAsync();

            // Execute/Dispatch tasks on schedule
            var tasks = await DependencyGroupRotations
                .Where(dgr => !dgr.HasExecuted)
                .Where(dgr => dgr.ExecutionTime < DateTimeOffset.UtcNow)
                .ToListAsync();

            foreach (var task in tasks)
            {
                var result = await svc.ExecuteAsync(
                    task.DependencyGroup.ValidPeriod,
                    async pwac =>
                    {
                        // invoked periodically and at completion
                        task.WorkflowActions = pwac;
                        if (pwac.HasBeenExecutedSuccessfully)
                            task.DependencyGroup.LastRotation = DateTimeOffset.UtcNow;
                        await SaveChangesAsync();
                    },
                    task.DependencyGroup.Resources.Select(r =>
                        r.Resource.Parameters).ToArray());
            }
        }

        private ValueConverter<T, string> GetJsonConverter<T>() =>
            new ValueConverter<T, string>(
                v => JsonConvert.SerializeObject(v),
                v => JsonConvert.DeserializeObject<T>(v));
    }
}
