// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Services;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace AuthJanitor.Functions
{
    public class ScheduleRekeyingTasks
    {
        private readonly ScheduleRekeyingTasksService _service;

        public ScheduleRekeyingTasks(ScheduleRekeyingTasksService service)
        {
            _service = service;
        }

        [FunctionName("ScheduleRekeyingTasks")]
        public async Task Run([TimerTrigger("0 */5 * * * *")]TimerInfo myTimer, ILogger log)
        {
            await _service.Run(myTimer, log);
        }
    }
}