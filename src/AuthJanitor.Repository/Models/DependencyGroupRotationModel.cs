// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthJanitor.Repository.Models
{
    [Table("DependencyGroupRotations")]
    public class DependencyGroupRotationModel
    {
        [Key]
        public Guid ResourceGroupRotationId { get; set; } = Guid.NewGuid();

        public DateTimeOffset ExecutionTime { get; set; }
        public bool HasExecuted { get; set; }

        public Guid DependencyGroupId { get; set; }
        public DependencyGroupModel DependencyGroup { get; set; }

        public DateTimeOffset LastRotated => WorkflowActions.FinishedExecution;

        [System.ComponentModel.DataAnnotations.Schema.NotMapped] // TODO!!!!
        public ProviderWorkflowActionCollection WorkflowActions { get; set; } =
            new ProviderWorkflowActionCollection();
    }
}
