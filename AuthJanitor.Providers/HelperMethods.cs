// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
using System.Security.Cryptography;
using System.Text;

namespace AuthJanitor.Providers
{
    public static class HelperMethods
    {
        private const string CHARS_ALPHANUMERIC_ONLY = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        public static string GenerateCryptographicallySecureString(int length, string chars = CHARS_ALPHANUMERIC_ONLY)
        {
            // Cryptography Tip!
            // https://cmvandrevala.wordpress.com/2016/09/24/modulo-bias-when-generating-random-numbers/
            // Using modulus to wrap around the source string tends to mathematically favor lower index values for
            //   smaller values of RAND_MAX (here it is LEN(chars)=62). To overcome this bias, we generate the randomness as
            //   4 bytes (int32) per single character we need, to maximize the value of RAND_MAX inside the RNG (as Int32.Max).
            //   Once the value comes out, though, we can introduce modulus again because RAND_MAX is based on the
            //   entropy going into the byte array rather than a fixed set (0,LEN(chars)) -- that makes it sufficiently
            //   large to overcome bias as seen by chi-squared. (Bias approaching zero)
            // * There is some evidence to suggest this has been taken into account in newer versions of NET. *
            // * The code below assumes this bias is *not* accounted for, which should make this secure generation xplat-safe for NET Core. *

            byte[] data = new byte[4 * length];
            using (RNGCryptoServiceProvider crypto = new RNGCryptoServiceProvider())
            {
                crypto.GetBytes(data);
            }

            StringBuilder sb = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                int randomNumber = BitConverter.ToInt32(data, i * 4);
                if (randomNumber < 0) randomNumber *= -1;
                sb.Append(chars[randomNumber % chars.Length]);
            }
            return sb.ToString();
        }

        public static string ToReadableString(this TimeSpan span, bool shortText = false)
        {
            string formatted = string.Format("{0}{1}{2}{3}{4}",
                span.Duration().Days > 30 ? string.Format(shortText ? "~{0:0}m " : "~{0:0} month{1}, ", span.Days / 30, (span.Days / 30) == 1 ? string.Empty : "s") : string.Empty,
                span.Duration().Days > 0 ? string.Format(shortText ? "{0:0}d " : "{0:0} day{1}, ", span.Days, span.Days == 1 ? string.Empty : "s") : string.Empty,
                span.Duration().Hours > 0 ? string.Format(shortText ? "{0:0}h " : "{0:0} hour{1}, ", span.Hours, span.Hours == 1 ? string.Empty : "s") : string.Empty,
                span.Duration().Minutes > 0 ? string.Format(shortText ? "{0:0}m " : "{0:0} minute{1}, ", span.Minutes, span.Minutes == 1 ? string.Empty : "s") : string.Empty,
                span.Duration().Seconds > 0 ? string.Format(shortText ? "{0:0}s" : "{0:0} second{1}", span.Seconds, span.Seconds == 1 ? string.Empty : "s") : string.Empty);

            if (formatted.EndsWith(", ")) formatted = formatted.Substring(0, formatted.Length - 2);

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

        public static string SHA256HashString(string str)
        {
            using (SHA256 sha256Hash = SHA256.Create())
                return ComputeHashString(sha256Hash, str);
        }

        public static string MD5HashString(string str)
        {
            using (MD5 md5Hash = MD5.Create())
                return ComputeHashString(md5Hash, str);
        }

        private static string ComputeHashString(HashAlgorithm alg, string str)
        {
            byte[] bytes = alg.ComputeHash(Encoding.UTF8.GetBytes(str));
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString("x2"));
            }

            return sb.ToString();
        }
    }
}
