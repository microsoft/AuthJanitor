// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Automation.Shared;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Threading.Tasks;

namespace AuthJanitor.Automation.EventSinks.SendGrid
{
    public class SendGridEventSink : IEventSink
    {
        private string _apiKey;
        private string _fromEmail;
        private string _fromName;
        private string[] _adminAddresses;
        private LogLevel _minLevel;

        private EmailAddress From => new EmailAddress(_fromEmail, _fromName);

        public SendGridEventSink(string apiKey, string fromEmail, string fromName, string[] adminAddresses, LogLevel minLevel = LogLevel.Warning)
        {
            _apiKey = apiKey;
            _fromEmail = fromEmail;
            _fromName = fromName;
            _adminAddresses = adminAddresses;
            _minLevel = minLevel;
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

            if (logLevel > _minLevel)
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

            if (_minLevel > LogLevel.Information && systemEvent == AuthJanitorSystemEvents.AnomalousEventOccurred)
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
                "<pre>" + JsonConvert.SerializeObject(detailObject, Formatting.Indented) + "</pre>";
            var nonHtmlTemplate =
                "AuthJanitor Event! (" + systemEvent.ToString() + ")" + Environment.NewLine +
                "Source: " + source + Environment.NewLine +
                "Object: " + Environment.NewLine +
                JsonConvert.SerializeObject(detailObject, Formatting.Indented);

            if (_minLevel > LogLevel.Information && systemEvent == AuthJanitorSystemEvents.AnomalousEventOccurred)
                return;
            else
                await SendMail("AuthJanitor Event", nonHtmlTemplate, htmlTemplate);
        }

        private async Task SendMail(string subject, string body, string htmlBody = default)
        {
            if (htmlBody == default) htmlBody = body;

            var client = new SendGridClient(_apiKey);
            foreach (var address in _adminAddresses)
            {
                var to = new EmailAddress(address);
                var email = MailHelper.CreateSingleEmail(From, to, subject, body, htmlBody);
                await client.SendEmailAsync(email);
            }
        }
    }
}