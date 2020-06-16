// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace AuthJanitor.UI.Shared.ViewModels
{
    public class ManagedSecretViewModel : IAuthJanitorViewModel
    {
        public Guid ObjectId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        [JsonIgnore]
        public TaskConfirmationStrategies TaskConfirmationStrategies
        {
            get => (TaskConfirmationStrategies)TaskConfirmationStrategiesInt;
            set => TaskConfirmationStrategiesInt = (int)value;
        }

        public int TaskConfirmationStrategiesInt { get; set; }

        public DateTimeOffset? LastChanged { get; set; }
        public int ValidPeriodMinutes { get; set; }

        [JsonIgnore]
        public TimeSpan ValidPeriod => TimeSpan.FromMinutes(ValidPeriodMinutes);

        public IEnumerable<string> AdminEmails { get; set; } = new List<string>();

        /// <summary>
        /// If the ManagedSecret is valid
        /// </summary>
        [JsonIgnore]
        public bool IsValid => Expiry > DateTimeOffset.UtcNow;

        /// <summary>
        /// Date/Time of expiry
        /// </summary>
        [JsonIgnore]
        public DateTimeOffset Expiry => LastChanged.GetValueOrDefault() + ValidPeriod;

        /// <summary>
        /// Time remaining until Expiry (if expired, TimeSpan.Zero)
        /// </summary>
        [JsonIgnore]
        public TimeSpan TimeRemaining => IsValid ? Expiry - DateTimeOffset.UtcNow : TimeSpan.Zero;

        [JsonIgnore]
        public string ProviderSummary => $"{Resources.Count(r => !r.IsRekeyableObjectProvider)} ALCs, " +
                                         $"{Resources.Count(r => r.IsRekeyableObjectProvider)} RKOs";

        [JsonIgnore]
        public int ExpiryPercent => IsValid ?
            (int)Math.Round(((ValidPeriod - TimeRemaining) / ValidPeriod) * 100, 0) :
            100;

        public string Nonce { get; set; }

        public string ResourceIds { get; set; } = string.Empty;
        public IEnumerable<ResourceViewModel> Resources { get; set; } = new List<ResourceViewModel>();

        public ManagedSecretViewModel()
        {
            ObjectId = Guid.Empty;
            Name = "New Managed Secret";
            Description = "Manages a secret between a rekeyable resource and application.";
            TaskConfirmationStrategies = TaskConfirmationStrategies.None;
            ValidPeriodMinutes = 60 * 24 * 90; // 90 days
            Nonce = string.Empty;
        }
    }
}
