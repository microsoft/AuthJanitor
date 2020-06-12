// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;

namespace AuthJanitor.Providers
{
    [Flags]
    public enum ProviderFeatureFlags : int
    {
        None                        = 0b00000000,

        IsTestable                  = 0b00000010,
        CanRotateWithoutDowntime    = 0b00000100,
        SupportsSecondaryKey        = 0b00001000,
        HasCandidateSelection       = 0b00010000
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ProviderAttribute : Attribute
    {
        /// <summary>
        /// Provider display name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// CSS class for Provider icons (FontAwesome 5)
        /// </summary>
        public string IconClass { get; set; }

        /// <summary>
        /// Provider display description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Features supported by this Provider
        /// </summary>
        public ProviderFeatureFlags Features { get; set; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ProviderImageAttribute : Attribute
    {
        /// <summary>
        /// SVG logo image for Provider
        /// </summary>
        public string SvgImage { get; set; }

        public ProviderImageAttribute(string svgImage) =>
            SvgImage = svgImage;
    }
}
