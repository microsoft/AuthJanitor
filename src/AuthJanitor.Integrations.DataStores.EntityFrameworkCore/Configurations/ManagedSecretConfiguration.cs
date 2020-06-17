using AuthJanitor.UI.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace AuthJanitor.Integrations.DataStores.EntityFrameworkCore.Configurations
{
    public class ManagedSecretConfiguration : IEntityTypeConfiguration<ManagedSecret>
    {
        public void Configure(EntityTypeBuilder<ManagedSecret> builder)
        {
            builder.HasKey(x => x.ObjectId);
            // TODO which properties are required?
            builder.Property(x => x.Name).IsRequired();
            builder.Property(x => x.AdminEmails)
                .HasConversion(
                    v => JsonConvert.SerializeObject(v),
                    v => JsonConvert.DeserializeObject<List<string>>(v));
            builder.Property(x => x.ResourceIds)
                .HasConversion(
                    v => JsonConvert.SerializeObject(v),
                    v => JsonConvert.DeserializeObject<List<Guid>>(v));
            // TODO: We have properties to ignore - are they ignored since they don't have a setter?
        }
    }
}
