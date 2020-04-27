// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace AuthJanitor.Automation.Shared.Models
{
    public class ManagedSecret : IAuthJanitorModel
    {
        public const int DEFAULT_NONCE_LENGTH = 64;

        public Guid ObjectId { get; set; } = Guid.NewGuid();
        public string Name { get; set; }
        public string Description { get; set; }

        public TaskConfirmationStrategies TaskConfirmationStrategies { get; set; } = TaskConfirmationStrategies.None;

        public DateTimeOffset LastChanged { get; set; } = DateTimeOffset.MinValue;
        public TimeSpan ValidPeriod { get; set; }

        public IEnumerable<string> AdminEmails { get; set; } = new List<string>();

        public string Nonce { get; set; } = HelperMethods.GenerateCryptographicallySecureString(DEFAULT_NONCE_LENGTH);

        public IEnumerable<Guid> ResourceIds { get; set; } = new List<Guid>();

        /// <summary>
        /// If the ManagedSecret is valid
        /// </summary>
        [JsonIgnore]
        public bool IsValid => Expiry < DateTimeOffset.UtcNow;

        /// <summary>
        /// Date/Time of expiry
        /// </summary>
        [JsonIgnore]
        public DateTimeOffset Expiry => LastChanged + ValidPeriod;

        /// <summary>
        /// Time remaining until Expiry (if expired, TimeSpan.Zero)
        /// </summary>
        [JsonIgnore]
        public TimeSpan TimeRemaining => IsValid ? Expiry - DateTimeOffset.UtcNow : TimeSpan.Zero;
    }
}
