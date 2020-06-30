// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;

namespace AuthJanitor.Integrity
{
    public class IntegrityReportSignature
    {
        public string Thumbprint { get; set; }
        public string Subject { get; set; }
        public string FriendlyName { get; set; }
        public string Issuer { get; set; }
        public string IssuerName { get; set; }
        public DateTime NotAfter { get; set; }
        public DateTime NotBefore { get; set; }
        public string SerialNumber { get; set; }
        public string SubjectName { get; set; }
        public string Version { get; set; }

        public string PublisherDescription { get; set; }
        public string PublisherUrl { get; set; }
    }
}
