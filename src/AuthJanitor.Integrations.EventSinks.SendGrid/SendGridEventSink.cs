// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.EventSinks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace AuthJanitor.Integrations.EventSinks.SendGrid
{
    public class SendGridEventSink : IEventSink
    {
        private SendGridEventSinkConfiguration Configuration { get; }

        public SendGridEventSink(
            IOptions<SendGridEventSinkConfiguration> configuration)
        {
            Configuration = configuration.Value;
        }

        public async Task LogEvent(LogLevel logLevel, string source, string eventMessage)
        {
            var htmlTemplate =
                "<h3>AuthJanitor Event!</h3><br />" +
                "<h6>Source: " + source + "</h6><br />" +
                "<em>" + DateTime.UtcNow + "</em>" +
                "<pre>" + eventMessage + "</pre>";
            var nonHtmlTemplate =
                "AuthJanitor Event!" + Environment.NewLine +
                "Source: " + source + Environment.NewLine +
                "Message: " + eventMessage + Environment.NewLine;

            if (logLevel > Configuration.MinimumLogLevel)
                await SendMail("AuthJanitor Event", nonHtmlTemplate, htmlTemplate);
        }

        public async Task LogEvent(AuthJanitorSystemEvents systemEvent, string source, string details)
        {
            var htmlTemplate =
                "<h3>AuthJanitor Event! (" + systemEvent.ToString() + ")</h3><br />" +
                "<h6>Source: " + source + "</h6><br />" +
                "<em>" + DateTime.UtcNow + "</em>" +
                "<pre>" + details + "</pre>";
            var nonHtmlTemplate =
                "AuthJanitor Event! (" + systemEvent.ToString() + ")" + Environment.NewLine +
                "Source: " + source + Environment.NewLine +
                "Message: " + details + Environment.NewLine;

            if (Configuration.MinimumLogLevel > LogLevel.Information && systemEvent == AuthJanitorSystemEvents.AnomalousEventOccurred)
                return;
            else
                await SendMail("AuthJanitor Event", nonHtmlTemplate, htmlTemplate);
        }

        public async Task LogEvent<T>(AuthJanitorSystemEvents systemEvent, string source, T detailObject)
        {
            var htmlTemplate =
                "<h3>AuthJanitor Event! (" + systemEvent.ToString() + ")</h3><br />" +
                "<h6>Source: " + source + "</h6><br />" +
                "<em>" + DateTime.UtcNow + "</em>" +
                "<pre>" + JsonSerializer.Serialize(detailObject, new JsonSerializerOptions() { WriteIndented = true }) + "</pre>";
            var nonHtmlTemplate =
                "AuthJanitor Event! (" + systemEvent.ToString() + ")" + Environment.NewLine +
                "Source: " + source + Environment.NewLine +
                "Object: " + Environment.NewLine +
                JsonSerializer.Serialize(detailObject, new JsonSerializerOptions() { WriteIndented = true });

            if (Configuration.MinimumLogLevel > LogLevel.Information && systemEvent == AuthJanitorSystemEvents.AnomalousEventOccurred)
                return;
            else
                await SendMail("AuthJanitor Event", nonHtmlTemplate, htmlTemplate);
        }

        private async Task SendMail(string subject, string body, string htmlBody = default)
        {
            if (htmlBody == default) htmlBody = body;

            var client = new SendGridClient(Configuration.ApiKey);

            // TODO: Better way of handling e-mail addresses ???
            foreach (var address in Configuration.AdminEmailAddresses)
            {
                var to = new EmailAddress(address);
                var email = MailHelper.CreateSingleEmail(
                    new EmailAddress(Configuration.FromEmail, Configuration.FromName),
                    to, subject, body, htmlBody);
                await client.SendEmailAsync(email);
            }
        }
    }
}