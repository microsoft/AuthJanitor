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
        /// Provider display description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// SVG logo image for Provider
        /// </summary>
        public string SvgImage { get; set; }
    }
}
