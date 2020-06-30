// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AuthJanitor.Integrity
{
    public class IntegrityReport
    {
        private const string AUTHJANITOR_LIBRARY_PREFIX = "AuthJanitor.";

        public enum IntegritySignatureCheckResult
        {
            Valid,
            NoSignature,
            BadDigest,
            UnknownProvider,
            UntrustedRoot,
            ExplicitDistrust
        }

        public string AssemblyName { get; set; }
        public string AssemblyVersion { get; set; }
        public string AssemblyPublicKeyToken { get; set; }
        public string LibraryFile { get; set; }
        public string LibraryFileHash { get; set; }

        public bool IsDynamic { get; set; }
        public bool IsFullTrust { get; set; }
        public bool IsSystemLibrary { get; set; }
        public bool IsSigned => SignatureCheckResult == IntegritySignatureCheckResult.Valid;

        public bool IsAuthJanitorNamedLibrary { get; set; }
        public bool IsAuthJanitorExtensionLibrary => AuthJanitorTypes.Any();
        public bool IsAuthJanitorProviderLibrary => AuthJanitorTypes.Any(t => t.ExtensibilityType == IntegrityReportExtensibilityType.ExtensibilityTypes.Provider);

        public List<IntegrityReportExtensibilityType> AuthJanitorTypes { get; set; } = new List<IntegrityReportExtensibilityType>();

        public IntegritySignatureCheckResult SignatureCheckResult { get; set; }

        public IEnumerable<IntegrityReportSignature> Signatures { get; set; } = new List<IntegrityReportSignature>();

        public IntegrityReport(Assembly assembly)
        {
            try
            {
                var asmName = assembly.GetName();
                AssemblyName = asmName.Name;
                AssemblyVersion = asmName.Version.ToString();
                AssemblyPublicKeyToken = BitConverter.ToString(asmName.GetPublicKeyToken());

                IsDynamic = assembly.IsDynamic;

                if (!IsDynamic)
                    LibraryFile = assembly.Location;

                IsFullTrust = assembly.IsFullyTrusted;
                
                IsAuthJanitorNamedLibrary = assembly.FullName.StartsWith(AUTHJANITOR_LIBRARY_PREFIX);
                IsSystemLibrary = assembly.GlobalAssemblyCache;

                AuthJanitorTypes = assembly.GetTypes()
                    .Where(t => typeof(IAuthJanitorExtensibilityPoint).IsAssignableFrom(t))
                    .Where(t => !t.IsInterface && !t.IsAbstract)
                    .Select(t => new IntegrityReportExtensibilityType(t))
                    .ToList();
            }
            catch (Exception ex) { }
        }
    }
}
