// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthJanitor.Repository.Models
{
    [Table("Resources")]
    public class ResourceModel
    {
        [Key]
        public Guid ResourceId { get; set; } = Guid.NewGuid();

        public string DescriptiveName { get; set; }
        public ProviderExecutionParameters Parameters { get; set; } =
            new ProviderExecutionParameters();

        public ICollection<ResourceDependencyGroupModel> DependencyGroups { get; set; }
                = new List<ResourceDependencyGroupModel>();
    }
}
