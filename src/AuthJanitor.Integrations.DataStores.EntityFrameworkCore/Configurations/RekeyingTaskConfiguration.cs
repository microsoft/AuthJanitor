using AuthJanitor.Providers;
using AuthJanitor.UI.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace AuthJanitor.Integrations.DataStores.EntityFrameworkCore.Configurations
{
    public class RekeyingTaskConfiguration : IEntityTypeConfiguration<RekeyingTask>
    {
        public void Configure(EntityTypeBuilder<RekeyingTask> builder)
        {
            builder.HasKey(x => x.ObjectId);
            // TODO which properties are required?
            builder.Property(x => x.Attempts)
                .HasConversion(
                    v => JsonConvert.SerializeObject(v),
                    v => JsonConvert.DeserializeObject<List<RekeyingAttemptLogger>>(v));
        }
    }
}
