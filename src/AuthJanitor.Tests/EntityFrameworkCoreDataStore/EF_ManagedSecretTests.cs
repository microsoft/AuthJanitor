// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.UI.Shared;
using AuthJanitor.UI.Shared.Models;
using System;

namespace AuthJanitor.Tests.EntityFrameworkCoreDataStore
{
    public class EF_ManagedSecretTests : EF_TestsBase<ManagedSecret>
    {
        protected override ManagedSecret CreateModel()
        {
            return new ManagedSecret()
            {
                ObjectId = Guid.NewGuid(),
                AdminEmails = new string[] { "admin@admin.com" },
                Description = "Description",
                Name = "Name",
                LastChanged = new DateTimeOffset(DateTime.Now),
                Nonce = "Nonce",
                TaskConfirmationStrategies = TaskConfirmationStrategies.AdminCachesSignOff,
                ValidPeriod = new TimeSpan(24, 0, 0),
                ResourceIds = new Guid[] { Guid.NewGuid() }
            };
        }

        protected override ManagedSecret UpdatedModel()
        {
            return new ManagedSecret()
            {
                ObjectId = model.ObjectId,
                AdminEmails = new string[] { "admin@admin.com" },
                Description = "Another Description",
                Name = "Another Name",
                LastChanged = new DateTimeOffset(DateTime.Now),
                Nonce = "Another Nonce",
                TaskConfirmationStrategies = TaskConfirmationStrategies.AdminCachesSignOff,
                ValidPeriod = new TimeSpan(24, 0, 0),
                ResourceIds = new Guid[] { Guid.NewGuid() }
            };
        }

        protected override bool CompareModel(ManagedSecret model1, ManagedSecret model2)
        {
            if (model1.ObjectId != model2.ObjectId ||
                model1.Description != model2.Description ||
                model1.Name != model2.Name ||
                model1.LastChanged != model2.LastChanged ||
                model1.Nonce != model2.Nonce ||
                model1.TaskConfirmationStrategies != model2.TaskConfirmationStrategies ||
                model1.ValidPeriod != model2.ValidPeriod)
                return false;
            if (!model1.AdminEmails.Equals(model2.AdminEmails))
                return false;
            if (!model1.ResourceIds.Equals(model2.ResourceIds))
                return false;
            return true;
        }
    }
}
