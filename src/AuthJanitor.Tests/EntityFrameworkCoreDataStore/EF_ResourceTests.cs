// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.UI.Shared.Models;
using System;

namespace AuthJanitor.Tests.EntityFrameworkCoreDataStore
{
    public class EF_ResourceTests : EF_TestsBase<Resource>
    {
        protected override Resource CreateModel()
        {
            return new Resource()
            {
                ObjectId = Guid.NewGuid(),
                Description = "Description",
                Name = "Name",
                ProviderConfiguration = "ProviderConfiguration",
                ProviderType = "ProviderType"
            };
        }

        protected override Resource UpdatedModel()
        {
            return new Resource()
            {
                ObjectId = model.ObjectId,
                Description = "Another Description",
                Name = "Another Name",
                ProviderConfiguration = "Another ProviderConfiguration",
                ProviderType = "Another ProviderType"
            };
        }

        protected override bool CompareModel(Resource model1, Resource model2)
        {
            if (model1.ObjectId != model2.ObjectId ||
                model1.Description != model2.Description ||
                model1.Name != model2.Name ||
                model1.ProviderConfiguration != model2.ProviderConfiguration ||
                model1.ProviderType != model2.ProviderType)
                return false;
            return true;
        }
    }
}
