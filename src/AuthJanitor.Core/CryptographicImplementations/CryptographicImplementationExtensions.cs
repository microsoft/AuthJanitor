// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
using System.Runtime.InteropServices;
using System.Security;

namespace AuthJanitor.Integrations.CryptographicImplementations
{
    public static class CryptographicImplementationExtensions
    {
        public static string GetNormalString(this SecureString str)
        {
            IntPtr bstr = Marshal.SecureStringToBSTR(str);
            try { return Marshal.PtrToStringBSTR(bstr); }
            finally { Marshal.ZeroFreeBSTR(bstr); }
        }

        /// <summary>
        /// Retrieve a SecureString object. Note that the existence of "str" at all makes this
        /// content inherently insecure in memory!
        /// </summary>
        /// <param name="str">String to convert</param>
        /// <returns></returns>
        public static SecureString GetSecureString(this string str)
        {
            var securePassword = new SecureString();
            foreach (char c in str)
                securePassword.AppendChar(c);
            securePassword.MakeReadOnly();
            return securePassword;
        }
    }
}