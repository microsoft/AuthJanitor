// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.UI.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace AuthJanitor.Integrations.DataStores.EntityFrameworkCore.Configurations
{
    public class ScheduleWindowConfiguration : IEntityTypeConfiguration<ScheduleWindow>
    {
        public void Configure(EntityTypeBuilder<ScheduleWindow> builder)
        {
            builder.HasKey(x => x.ObjectId);
            builder.Property(x => x.CronStrings).IsRequired();
            builder.Property(x => x.CronStrings)
                .HasConversion(
                    v => JsonConvert.SerializeObject(v),
                    v => JsonConvert.DeserializeObject<List<string>>(v));
        }
    }
}
