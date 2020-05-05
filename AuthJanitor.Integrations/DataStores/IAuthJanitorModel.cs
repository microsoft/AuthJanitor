// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;

namespace AuthJanitor.Integrations.DataStores
{
    public interface IAuthJanitorModel
    {
        /// <summary>
        /// Unique Object Identifier
        /// </summary>
        Guid ObjectId { get; set; }
    }
}
