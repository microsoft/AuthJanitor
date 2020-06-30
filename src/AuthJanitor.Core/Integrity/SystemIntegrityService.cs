// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthenticodeExaminer;
using AuthJanitor.Integrations.CryptographicImplementations;
using AuthJanitor.Providers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace AuthJanitor.Integrity
{
    public class SystemIntegrityService
    {
        private readonly ICryptographicImplementation _cryptographicImplementation;
        private readonly AuthJanitorCoreConfiguration _authJanitorCoreConfiguration;
        private readonly ILogger<SystemIntegrityService> _logger;

        public SystemIntegrityService(
            ICryptographicImplementation cryptographicImplementation,
            AuthJanitorCoreConfiguration authJanitorCoreConfiguration,
            ILogger<SystemIntegrityService> logger)
        {
            _cryptographicImplementation = cryptographicImplementation;
            _authJanitorCoreConfiguration = authJanitorCoreConfiguration;
            _logger = logger;
        }

        public async Task PerformInitialIntegrityCheck()
        {
            _logger.LogInformation("Performing warmup system integrity checks...");
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => new IntegrityReport(assembly))
                .ToList();

            _logger.LogInformation("System has loaded {numberOfAssemblies} assemblies", loadedAssemblies.Count);
            if (_authJanitorCoreConfiguration.EnforceProviderSignature)
            {
                CheckSignatureForAssignableType<IAuthJanitorProvider>(loadedAssemblies,
                    "Provider(s) are not properly signed: {providers}",
                    "One or more provider(s) were not properly signed; aborting.",
                    "All providers are signed and valid");
            }
            if (_authJanitorCoreConfiguration.EnforceAllExtensibilitySignatures)
            {
                CheckSignatureForAssignableType<IAuthJanitorExtensibilityPoint>(loadedAssemblies,
                    "Extensibility point(s) are not properly signed: {extensibilityPoints}",
                    "One or more extensibility point(s) were not properly signed; aborting.",
                    "All extensibility points are signed and valid");
            }
            if (_authJanitorCoreConfiguration.EnforceAllSignatures)
            {
                CheckSignatureForAssignableType<IAuthJanitorIntegrityIncluded>(loadedAssemblies);
            }

            if (_authJanitorCoreConfiguration.EnforceSingleIssuer)
            {
                var uniqueIssuers = loadedAssemblies.Where(a => a.IsAuthJanitorNamedLibrary)
                    .Select(a => a.Signatures
                        .Select(s => s.IssuerName).Distinct())
                    .Distinct()
                    .Count();
                if (uniqueIssuers > 1)
                {
                    _logger.LogCritical("Multiple issuers detected and single-issuer is enforced; aborting.");
                    Environment.FailFast("Multiple issuers detected and single-issuer is enforced; aborting.");
                }
            }

            await Task.Yield();
        }

        public async Task<IEnumerable<IntegrityReport>> GetIntegrityReports()
        {
            var hashes = new HashSet<string>();
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a =>
                {
                    if (hashes.Contains(a.FullName))
                        return null;
                    hashes.Add(a.FullName);
                    return new IntegrityReport(a);
                })
                .Where(a => a != null)
                .ToList();

            await Task.WhenAll(loadedAssemblies.Select(async asm =>
            {
                if (string.IsNullOrEmpty(asm.LibraryFile))
                    return;

                asm.LibraryFileHash = await _cryptographicImplementation.HashFile(asm.LibraryFile);

                var inspector = new FileInspector(asm.LibraryFile);
                var signatureCheckResult = inspector.Validate();
                asm.SignatureCheckResult = Enum.Parse<IntegrityReport.IntegritySignatureCheckResult>(signatureCheckResult.ToString());

                if (signatureCheckResult != SignatureCheckResult.NoSignature &&
                    signatureCheckResult != SignatureCheckResult.BadDigest)
                {
                    asm.Signatures = inspector.GetSignatures().Select(s =>
                    {
                        return new IntegrityReportSignature()
                        {
                            Thumbprint = s.SigningCertificate.Thumbprint,
                            Subject = s.SigningCertificate.Subject,
                            FriendlyName = s.SigningCertificate.FriendlyName,
                            Issuer = s.SigningCertificate.Issuer,
                            IssuerName = s.SigningCertificate.IssuerName?.Name,
                            NotAfter = s.SigningCertificate.NotAfter,
                            NotBefore = s.SigningCertificate.NotBefore,
                            SerialNumber = s.SigningCertificate.SerialNumber,
                            SubjectName = s.SigningCertificate.SubjectName?.Name,
                            Version = s.SigningCertificate.Version.ToString(),
                            
                            PublisherDescription = s.PublisherInformation?.Description,
                            PublisherUrl = s.PublisherInformation?.UrlLink
                        };
                    }).ToList();
                }
            }));

            return loadedAssemblies;
        }

        private void CheckSignatureForAssignableType<T>(
            IEnumerable<IntegrityReport> loadedAssemblies,
            string errorMessage = "One or more items were not signed: {items}",
            string environmentErrorMessage = "One or more items were not signed; aborting.",
            string successMessage = "All items are signed and valid")
        {
            var unsigned = loadedAssemblies
                .Where(a => a.AuthJanitorTypes.Any(t => typeof(T).IsAssignableFrom(t.Type)))
                .Where(a => !a.IsSigned);

            if (unsigned.Any())
            {
                _logger.LogCritical(errorMessage, string.Join(", ", unsigned.Select(p => Path.GetFileName(p.LibraryFile))));
                Environment.FailFast(environmentErrorMessage);
            }
            else
            {
                _logger.LogInformation(successMessage);
            }
        }
    }
}
