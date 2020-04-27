// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;

namespace AuthJanitor.Automation.Shared.Models
{
    public interface IAuthJanitorModel
    {
        /// <summary>
        /// Unique Object Identifier
        /// </summary>
        Guid ObjectId { get; set; }
    }
}
