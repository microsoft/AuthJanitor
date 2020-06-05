// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.UI.Shared;
using AuthJanitor.UI.Shared.MetaServices;
using AuthJanitor.UI.Shared.Models;
using AuthJanitor.Integrations.DataStores;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace AuthJanitor
{
    public class PerformAutoRekeyingTasks
    {
        private readonly AuthJanitorCoreConfiguration _configuration;
        private readonly TaskExecutionMetaService _taskExecutionMetaService;

        private readonly IDataStore<RekeyingTask> _rekeyingTasks;

        public PerformAutoRekeyingTasks(
            IOptions<AuthJanitorCoreConfiguration> configuration,
            TaskExecutionMetaService taskExecutionMetaService,
            IDataStore<RekeyingTask> rekeyingTaskStore)
        {
            _configuration = configuration.Value;
            _taskExecutionMetaService = taskExecutionMetaService;
            _rekeyingTasks = rekeyingTaskStore;
        }

        [FunctionName("PerformAutoRekeyingTasks")]
        public async Task Run([TimerTrigger("0 */2 * * * *")]TimerInfo myTimer, ILogger log)
        {
            _ = myTimer; // unused but required for attribute

            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            var toRekey = await _rekeyingTasks.Get(t =>
                (t.ConfirmationType == TaskConfirmationStrategies.AdminCachesSignOff ||
                 t.ConfirmationType == TaskConfirmationStrategies.AutomaticRekeyingAsNeeded ||
                 t.ConfirmationType == TaskConfirmationStrategies.AutomaticRekeyingScheduled) &&
                DateTimeOffset.UtcNow + TimeSpan.FromHours(_configuration.AutomaticRekeyableJustInTimeLeadTimeHours) > t.Expiry);

            foreach (var task in toRekey)
            {
                await _taskExecutionMetaService.ExecuteTask(task.ObjectId);
            }
        }
    }
}
