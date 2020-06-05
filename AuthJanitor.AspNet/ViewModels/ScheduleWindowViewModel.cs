// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
using System.Collections.Generic;
//using System.Linq;

namespace AuthJanitor.UI.Shared.ViewModels
{
    public class ScheduleWindowViewModel : IAuthJanitorViewModel
    {
        public Guid ObjectId { get; set; }

        public IEnumerable<string> CronStrings { get; set; } = new List<string>();

        //[JsonIgnore]
        //public DateTimeOffset NextOccurrence =>
        //    CronStrings.Select(s => CronExpression.Parse(s))
        //               .Select(c => c.GetNextOccurrence(DateTimeOffset.Now, TimeZoneInfo.Utc).GetValueOrDefault())
        //               .OrderBy(c => c.Ticks)
        //               .First();
    }
}
