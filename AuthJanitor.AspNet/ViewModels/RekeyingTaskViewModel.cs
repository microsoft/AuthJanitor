// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers;
using System;
using System.Collections.Generic;

namespace AuthJanitor.UI.Shared.ViewModels
{
    public class RekeyingTaskViewModel : IAuthJanitorViewModel
    {
        public Guid ObjectId { get; set; }

        public DateTimeOffset Queued { get; set; }
        public DateTimeOffset Expiry { get; set; }

        public bool RekeyingInProgress { get; set; } = false;
        public bool RekeyingCompleted { get; set; } = false;
        public string RekeyingErrorMessage { get; set; } = string.Empty;

        public TaskConfirmationStrategies ConfirmationType { get; set; }

        public string PersistedCredentialUser { get; set; }

        public Guid AvailabilityScheduleId { get; set; }

        public ManagedSecretViewModel ManagedSecret { get; set; } = new ManagedSecretViewModel();

        public List<RekeyingAttemptLogger> Attempts { get; set; } = new List<RekeyingAttemptLogger>();
    }
}
