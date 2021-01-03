// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthJanitor.Repository.Models
{
    [Table("DependencyGroups")]
    public class DependencyGroupModel
    {
        [Key]
        public Guid DependencyGroupId { get; set; } = Guid.NewGuid();
        
        public string Name { get; set; }
        public TimeSpan ValidPeriod { get; set; }
        public DateTimeOffset LastRotation { get; set; } = DateTimeOffset.MinValue;

        public ProviderExecutionParameters DefaultParameters { get; set; } =
            new ProviderExecutionParameters();

        public ICollection<ResourceDependencyGroupModel> Resources { get; set; }
                = new List<ResourceDependencyGroupModel>();

        public ICollection<DependencyGroupRotationModel> Rotations { get; set; }
                = new List<DependencyGroupRotationModel>();
    }
}
