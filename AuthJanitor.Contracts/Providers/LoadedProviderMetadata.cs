// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
using System.Reflection;

namespace AuthJanitor.Providers
{
    public class LoadedProviderMetadata
    {
        /// <summary>
        /// Library file where Provider was loaded from
        /// </summary>
        public string OriginatingFile { get; set; }

        /// <summary>
        /// NET Type name for Provider
        /// </summary>
        public string ProviderTypeName { get; set; }

        /// <summary>
        /// NET Type for Provider
        /// </summary>
        public Type ProviderType { get; set; }

        /// <summary>
        /// NET Type for Provider's expected Configuration
        /// </summary>
        public Type ProviderConfigurationType { get; set; }

        /// <summary>
        /// If the Provider is a RekeyableObjectProvider
        /// </summary>
        public bool IsRekeyableObjectProvider => typeof(IRekeyableObjectProvider).IsAssignableFrom(ProviderType);

        /// <summary>
        /// Metadata about the Provider
        /// </summary>
        public ProviderAttribute Details { get; set; }

        /// <summary>
        /// Provider SVG logo image
        /// </summary>
        public string SvgImage { get; set; }

        /// <summary>
        /// NET Assembly where the Provider was loaded from
        /// </summary>
        public AssemblyName AssemblyName { get; set; }
    }
}
