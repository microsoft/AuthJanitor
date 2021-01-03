// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthJanitor.Repository.Models
{
    [Table("ResourceDependencyGroups")]
    public class ResourceDependencyGroupModel
    {
        public Guid ResourceId { get; set; }
        public Guid DependencyGroupId { get; set; }

        public ResourceModel Resource { get; set; }
        public DependencyGroupModel DependencyGroup { get; set; }
    }
}
