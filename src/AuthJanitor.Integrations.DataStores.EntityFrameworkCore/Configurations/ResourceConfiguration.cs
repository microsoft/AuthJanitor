// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.UI.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthJanitor.Integrations.DataStores.EntityFrameworkCore.Configurations
{
    public class ResourceConfiguration : IEntityTypeConfiguration<Resource>
    {
        public void Configure(EntityTypeBuilder<Resource> builder)
        {
            builder.HasKey(x => x.ObjectId);
            // TODO which properties are required?
            builder.Property(x => x.Name).IsRequired();
            builder.Property(x => x.ProviderType).IsRequired();
        }
    }
}
