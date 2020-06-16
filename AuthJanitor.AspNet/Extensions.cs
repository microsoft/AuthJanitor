// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;

namespace AuthJanitor.UI.Shared
{
    public static class Extensions
    {
        public static string ToReadableString(this TimeSpan span, bool shortText = false)
        {
            string formatted = string.Format("{0}{1}{2}{3}{4}",
                span.Duration().Days > 30 ? string.Format(shortText ? "~{0:0}m " : "~{0:0} month{1}, ", span.Days / 30, (span.Days / 30) == 1 ? string.Empty : "s") : string.Empty,
                span.Duration().Days > 0 ? string.Format(shortText ? "{0:0}d " : "{0:0} day{1}, ", span.Days, span.Days == 1 ? string.Empty : "s") : string.Empty,
                span.Duration().Hours > 0 ? string.Format(shortText ? "{0:0}h " : "{0:0} hour{1}, ", span.Hours, span.Hours == 1 ? string.Empty : "s") : string.Empty,
                span.Duration().Minutes > 0 ? string.Format(shortText ? "{0:0}m " : "{0:0} minute{1}, ", span.Minutes, span.Minutes == 1 ? string.Empty : "s") : string.Empty,
                span.Duration().Seconds > 0 ? string.Format(shortText ? "{0:0}s" : "{0:0} second{1}", span.Seconds, span.Seconds == 1 ? string.Empty : "s") : string.Empty);

            if (formatted.EndsWith(", ")) formatted = formatted[0..^2];

            if (string.IsNullOrEmpty(formatted)) formatted = "Enter a duration in minutes.";

            return formatted;
        }

        public static T GetEnumValueAttribute<T>(this Enum enumVal) where T : Attribute
        {
            var attrib = enumVal.GetType()
                   .GetMember(enumVal.ToString())[0]
                   .GetCustomAttributes(typeof(T), false);
            return (attrib.Length > 0) ? (T)attrib[0] : null;
        }
    }
}
