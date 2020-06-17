using AuthJanitor.Providers;
using AuthJanitor.UI.Shared;
using AuthJanitor.UI.Shared.Models;
using System;
using System.Collections.Generic;

namespace AuthJanitor.Tests.EntityFrameworkCoreDataStore
{
    public class EF_RekeyingTaskTests : EF_TestsBase<RekeyingTask>
    {
        protected override RekeyingTask CreateModel()
        {
            return new RekeyingTask()
            {
                ObjectId = Guid.NewGuid(),
                AvailabilityScheduleId = Guid.NewGuid(),
                Attempts = new List<RekeyingAttemptLogger>()
                {
                    new RekeyingAttemptLogger()
                    {
                        AttemptStarted = new DateTimeOffset(DateTime.Now),
                        AttemptFinished = new DateTimeOffset(DateTime.Now),
                        ChainedLogger = null,
                        LogString = "LogString",
                        OuterException = "OuterException",
                        UserDisplayName = "UserName",
                        UserEmail = "user@email.com"
                    }
                },
                ConfirmationType = TaskConfirmationStrategies.AdminCachesSignOff,
                Expiry = new DateTimeOffset(DateTime.Now),
                ManagedSecretId = Guid.NewGuid(),
                PersistedCredentialId = Guid.NewGuid(),
                PersistedCredentialUser = "CredentialUser",
                RekeyingInProgress = false,
                Queued = new DateTimeOffset(DateTime.Now),
                RekeyingCompleted = false,
                RekeyingFailed = true
            };
        }

        protected override RekeyingTask UpdatedModel()
        {
            return new RekeyingTask()
            {
                ObjectId = model.ObjectId,
                AvailabilityScheduleId = Guid.NewGuid(),
                Attempts = new List<RekeyingAttemptLogger>()
                {
                    new RekeyingAttemptLogger()
                    {
                        AttemptStarted = new DateTimeOffset(DateTime.Now),
                        AttemptFinished = new DateTimeOffset(DateTime.Now),
                        ChainedLogger = null,
                        LogString = "Another LogString",
                        OuterException = "Another OuterException",
                        UserDisplayName = "Another UserName",
                        UserEmail = "user2@email.com"
                    }
                },
                ConfirmationType = TaskConfirmationStrategies.AdminCachesSignOff,
                Expiry = new DateTimeOffset(DateTime.Now),
                ManagedSecretId = Guid.NewGuid(),
                PersistedCredentialId = Guid.NewGuid(),
                PersistedCredentialUser = "Another CredentialUser",
                RekeyingInProgress = false,
                Queued = new DateTimeOffset(DateTime.Now),
                RekeyingCompleted = false,
                RekeyingFailed = true
            };
        }

        protected override bool CompareModel(RekeyingTask model1, RekeyingTask model2)
        {
            if (model1.ObjectId != model2.ObjectId ||
                    model1.AvailabilityScheduleId != model2.AvailabilityScheduleId ||
                    model1.Attempts != model2.Attempts ||
                    model1.ConfirmationType != model2.ConfirmationType ||
                    model1.Expiry != model2.Expiry ||
                    model1.ManagedSecretId != model2.ManagedSecretId ||
                    model1.PersistedCredentialId != model2.PersistedCredentialId ||
                    model1.PersistedCredentialUser != model2.PersistedCredentialUser ||
                    model1.RekeyingInProgress != model2.RekeyingInProgress ||
                    model1.Queued != model2.Queued ||
                    model1.RekeyingCompleted != model2.RekeyingCompleted ||
                    model1.RekeyingFailed != model2.RekeyingFailed)
                return false;
            if (!model1.Attempts.Equals(model2.Attempts))
                return false;
            return true;
        }
    }
}
