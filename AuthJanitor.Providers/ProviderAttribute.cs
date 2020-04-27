// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;

namespace AuthJanitor.Providers
{
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
        /// URL with more information about the Provider
        /// </summary>
        public string MoreInformationUrl { get; set; }
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
