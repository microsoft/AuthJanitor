// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AuthJanitor.Automation.Cryptography.Default
{
    /// <summary>
    /// The default AuthJanitor cryptographic implementation attempts to be FIPS-aware
    /// </summary>
    public class DefaultCryptographicImplementation : ICryptographicImplementation
    {
        private const string CHARS_ALPHANUMERIC_ONLY = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private string _masterKey;
        public DefaultCryptographicImplementation(string masterKey)
        {
            _masterKey = masterKey;
        }

        public Task<string> GenerateCryptographicallySecureString(int length)
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

            var chars = CHARS_ALPHANUMERIC_ONLY;
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
            return Task.FromResult(sb.ToString());
        }

        public Task<string> Hash(string str) => Hash(Encoding.UTF8.GetBytes(str));

        public Task<string> Hash(byte[] inputBytes)
        {
            byte[] bytes = SHA256.Create().ComputeHash(inputBytes);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString("x2"));
            }

            return Task.FromResult(sb.ToString());
        }

        public async Task<string> Decrypt(string salt, string cipherText)
        {
            using (MemoryStream ms = new MemoryStream(Convert.FromBase64String(cipherText)))
            using (Aes aes = Aes.Create())
            {
                aes.Key = new Rfc2898DeriveBytes(_masterKey, Encoding.UTF8.GetBytes(salt)).GetBytes(128 / 8);
                aes.IV = ReadByteArray(ms);
                CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
                StreamReader reader = new StreamReader(cs, Encoding.Unicode);
                try
                {
                    string retval = await reader.ReadToEndAsync();
                    reader.Dispose();
                    cs.Dispose();
                    return retval;
                }
                catch (Exception e)
                {
                    throw e;
                }
            }
        }

        public async Task<string> Encrypt(string salt, string plainText)
        {
            using (MemoryStream ms = new MemoryStream())
            using (Aes aes = Aes.Create())
            {
                aes.Key = new Rfc2898DeriveBytes(_masterKey, Encoding.UTF8.GetBytes(salt)).GetBytes(128 / 8);
                ms.Write(BitConverter.GetBytes(aes.IV.Length), 0, sizeof(int));
                ms.Write(aes.IV, 0, aes.IV.Length);

                using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write, true))
                {
                    var plainTextBytes = Encoding.Unicode.GetBytes(plainText);
                    await cs.WriteAsync(plainTextBytes, 0, plainTextBytes.Length);
                    cs.FlushFinalBlock();
                }

                ms.Seek(0, SeekOrigin.Begin);
                return Convert.ToBase64String(ms.ToArray());
            }
        }

        private static byte[] ReadByteArray(Stream s)
        {
            byte[] rawLength = new byte[sizeof(int)];
            if (s.Read(rawLength, 0, rawLength.Length) != rawLength.Length)
            {
                throw new SystemException("Stream did not contain properly formatted byte array");
            }

            byte[] buffer = new byte[BitConverter.ToInt32(rawLength, 0)];
            if (s.Read(buffer, 0, buffer.Length) != buffer.Length)
            {
                throw new SystemException("Did not read byte array properly");
            }

            return buffer;
        }
    }
}
