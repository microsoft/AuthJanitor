// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers;

namespace AuthJanitor.UI.Shared.ViewModels
{
    public class LoadedProviderViewModel : IAuthJanitorViewModel
    {
        public string OriginatingFile { get; set; }
        public string ProviderTypeName { get; set; }
        public bool IsRekeyableObjectProvider { get; set; }
        public ProviderAttribute Details { get; set; }
        public string SvgImage { get; set; }

        public string AssemblyVersion { get; set; }
        public byte[] AssemblyPublicKey { get; set; }
        public byte[] AssemblyPublicKeyToken { get; set; }
    }
}
