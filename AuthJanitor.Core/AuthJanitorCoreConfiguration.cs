// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.ComponentModel;

namespace AuthJanitor
{
    public class AuthJanitorCoreConfiguration
    {
        [Description("Number of hours prior to an automatic rekeying when a RekeyingTask is created")]
        public int AutomaticRekeyableTaskCreationLeadTimeHours { get; set; } = 24 * 3;

        [Description("Number of hours prior to expiry when a just-in-time automatic rekeying is attempted")]
        public int AutomaticRekeyableJustInTimeLeadTimeHours { get; set; } = 24 * 1;

        [Description("Number of hours prior to expiry of an approval-required rekeying when a notification is sent to administrators")]
        public int ApprovalRequiredLeadTimeHours { get; set; } = 24 * 7;

        [Description("Number of hours prior to expiry when an external signal will attempt a rekeying")]
        public int ExternalSignalRekeyableLeadTimeHours { get; set; } = 24 * 2;

        [Description("Length of Secret version nonce")]
        public int DefaultNonceLength { get; set; } = 64;
    }
}
