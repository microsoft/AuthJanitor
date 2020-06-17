using AuthJanitor.UI.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthJanitor.Integrations.DataStores.EntityFrameworkCore
{
    public class AuthJanitorDbContext : DbContext
    {
        public AuthJanitorDbContext()
        {
        }

        public AuthJanitorDbContext(DbContextOptions<AuthJanitorDbContext> dbContextOptions) : base(dbContextOptions)
        {
        }

        public DbSet<ManagedSecret> ManagedSecrets { get; set; }
        public DbSet<RekeyingTask> RekeyingTasks { get; set; }
        public DbSet<Resource> Resources { get; set; }
        public DbSet<ScheduleWindow> ScheduleWindows { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        }
    }
}
