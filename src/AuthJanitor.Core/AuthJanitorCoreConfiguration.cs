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

        [Description("Enforce a single issuer for all AuthJanitor modules")]
        public bool EnforceSingleIssuer { get; set; } = false;

        [Description("Require that all Providers be signed")]
        public bool EnforceProviderSignature { get; set; } = false;

        [Description("Require that all Providers/Integrations be signed")]
        public bool EnforceAllExtensibilitySignatures { get; set; } = false;

        [Description("Require that everything is signed")]
        public bool EnforceAllSignatures { get; set; } = false;
    }
}
