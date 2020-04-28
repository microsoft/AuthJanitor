// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.ComponentModel;

namespace AuthJanitor.Automation.Shared
{
    public class AuthJanitorServiceConfiguration
    {
        [Description("Number of hours prior to an automatic rekeying when a RekeyingTask is created")]
        public int AutomaticRekeyableTaskCreationLeadTimeHours { get; set; }

        [Description("Number of hours prior to expiry when a just-in-time automatic rekeying is attempted")]
        public int AutomaticRekeyableJustInTimeLeadTimeHours { get; set; }

        [Description("Number of hours prior to expiry of an approval-required rekeying when a notification is sent to administrators")]
        public int ApprovalRequiredLeadTimeHours { get; set; }

        [Description("Number of hours prior to expiry when an external signal will attempt a rekeying")]
        public int ExternalSignalRekeyableLeadTimeHours { get; set; }

        // -----

        [Description("Name of container which holds secure persistence objects")]
        public string SecurePersistenceContainerName { get; set; }

        [Description("Master encryption key")]
        public string MasterEncryptionKey { get; set; }

        // -----

        [Description("Name of storage container which holds AuthJanitor metadata")]
        public string MetadataStorageContainerName { get; set; } = "authjanitor";

        // -----

        [Description("Length of Secret version nonce")]
        public int DefaultNonceLength { get; set; } = 64;
    }
}
