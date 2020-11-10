// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.Collections.Generic;
using AuthJanitor.Providers;

namespace AuthJanitor.UI.Shared.ViewModels
{
    public enum ProviderCapabilities
    {
        None,
        CanCleanup,
        CanDistributeLongTermSecrets,
        CanDistributeTemporarySecrets,
        CanEnumerateResourceCandidates,
        CanGenerateTemporarySecrets,
        CanPerformUnifiedCommits,
        CanPerformUnifiedCommitForTemporarySecret,
        CanRekey,
        CanRunSanityTests
    }

    public class LoadedProviderViewModel : IAuthJanitorViewModel
    {
        public string OriginatingFile { get; set; }
        public string ProviderTypeName { get; set; }
        public bool IsRekeyableObjectProvider { get; set; }
        public ProviderAttribute Details { get; set; }
        public IEnumerable<ProviderCapabilities> Capabilities { get; set; } = new[] { ProviderCapabilities.None };
        public string AssemblyVersion { get; set; }
    }
}
