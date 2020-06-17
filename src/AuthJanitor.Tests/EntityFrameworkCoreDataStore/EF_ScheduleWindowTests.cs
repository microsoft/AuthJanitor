// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.UI.Shared.Models;
using System;
using System.Linq;

namespace AuthJanitor.Tests.EntityFrameworkCoreDataStore
{
    public class EF_ScheduleWindowTests : EF_TestsBase<ScheduleWindow>
    {
        protected override ScheduleWindow CreateModel()
        {
            return new ScheduleWindow()
            {
                ObjectId = Guid.NewGuid(),
                CronStrings = new string[] { "TODO" }
            };
        }

        protected override ScheduleWindow UpdatedModel()
        {
            return new ScheduleWindow()
            {
                ObjectId = model.ObjectId,
                CronStrings = new string[] { "TODO2" }
            };
        }

        protected override bool CompareModel(ScheduleWindow model1, ScheduleWindow model2)
        {
            if (model1.ObjectId != model2.ObjectId)
                return false;
            if (model1.CronStrings.Count() != model2.CronStrings.Count())
                return false;
            if (!model1.CronStrings.Equals(model2.CronStrings))
                return false;
            return true;
        }
    }
}
