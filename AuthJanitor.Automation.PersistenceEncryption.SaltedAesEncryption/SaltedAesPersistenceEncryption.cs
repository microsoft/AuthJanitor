// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Automation.Shared;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AuthJanitor.Automation.PersistenceEncryption.SaltedAesEncryption
{
    public class SaltedAesPersistenceEncryption : IPersistenceEncryption
    {
        private string _key;
        public SaltedAesPersistenceEncryption(string key)
        {
            _key = key;
        }

        public async Task<string> Decrypt(string salt, string cipherText)
        {
            using (MemoryStream ms = new MemoryStream(Convert.FromBase64String(cipherText)))
            using (Aes aes = new AesManaged())
            {
                aes.Key = new Rfc2898DeriveBytes(_key, Encoding.UTF8.GetBytes(salt)).GetBytes(128 / 8);
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
            using (Aes aes = new AesManaged())
            {
                aes.Key = new Rfc2898DeriveBytes(_key, Encoding.UTF8.GetBytes(salt)).GetBytes(128 / 8);
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
