// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace AuthJanitor.Integrations.EventSinks.SendGrid
{
    public class SendGridEventSinkConfiguration
    {
        [Description("SendGrid API Key")]
        public string ApiKey { get; set; }

        [Description("Address of E-mail sender")]
        public string FromEmail { get; set; }

        [Description("Name of E-mail sender")]
        public string FromName { get; set; }

        [Description("Minimum log level to dispatch")]
        public LogLevel MinimumLogLevel { get; set; }

        [Description("Admin E-mails")]
        public string[] AdminEmailAddresses { get; set; }
    }
}
