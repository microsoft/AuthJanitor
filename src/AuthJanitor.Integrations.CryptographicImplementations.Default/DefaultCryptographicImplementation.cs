// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AuthJanitor.Integrations.CryptographicImplementations.Default
{
    /// <summary>
    /// The default AuthJanitor cryptographic implementation.
    /// 
    /// This implementation uses cryptographic Types which pass through to OS modules which can be enabled for FIPS.
    /// https://docs.microsoft.com/en-us/dotnet/standard/security/fips-compliance
    /// </summary>
    public class DefaultCryptographicImplementation : ICryptographicImplementation
    {
        private const string CHARS_ALPHANUMERIC_ONLY = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        private DefaultCryptographicImplementationConfiguration Configuration { get; }

        /// <summary>
        /// The default AuthJanitor cryptographic implementation
        /// </summary>
        public DefaultCryptographicImplementation(
            IOptions<DefaultCryptographicImplementationConfiguration> configuration)
        {
            Configuration = configuration.Value;
        }

        /// <summary>
        /// Generates a cryptographically random SecureString of a given length.
        /// 
        /// This implementation uses RNGCryptoServiceProvider.
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        public Task<SecureString> GenerateCryptographicallyRandomSecureString(int length)
        {
            var secureString = new SecureString();
            GenerateCryptographicallyRandomCharacters((c) => secureString.AppendChar(c), length);
            return Task.FromResult(secureString);
        }

        /// <summary>
        /// Generates a cryptographically random string of a given length.
        /// 
        /// This implementation uses RNGCryptoServiceProvider.
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        public Task<string> GenerateCryptographicallyRandomString(int length)
        {
            var sb = new StringBuilder();
            GenerateCryptographicallyRandomCharacters((c) => sb.Append(c), length);
            return Task.FromResult(sb.ToString());
        }

        private void GenerateCryptographicallyRandomCharacters(Action<char> characterOutputAction, int numChars)
        {
            // https://cmvandrevala.wordpress.com/2016/09/24/modulo-bias-when-generating-random-numbers/
            var chars = CHARS_ALPHANUMERIC_ONLY;
            byte[] data = new byte[4 * numChars];
            using (RNGCryptoServiceProvider crypto = new RNGCryptoServiceProvider())
            {
                crypto.GetBytes(data);
            }

            for (int i = 0; i < numChars; i++)
            {
                int randomNumber = BitConverter.ToInt32(data, i * 4);
                if (randomNumber < 0) randomNumber *= -1;
                characterOutputAction(chars[randomNumber % chars.Length]);
            }
        }

        /// <summary>
        /// Generates a one-way hash of a given string.
        /// 
        /// This implementation uses SHA256.
        /// </summary>
        /// <param name="str">String to hash</param>
        /// <returns>SHA256 hash of string</returns>
        public Task<string> Hash(string str) => Hash(Encoding.UTF8.GetBytes(str));

        /// <summary>
        /// Generates a one-way hash of a given byte array.
        /// 
        /// This implementation uses SHA256.Create()
        /// </summary>
        /// <param name="inputBytes">Bytes to hash</param>
        /// <returns>SHA256 hash of byte array</returns>
        public Task<string> Hash(byte[] inputBytes)
        {
            byte[] bytes = SHA256.Create().ComputeHash(inputBytes);
            return Task.FromResult(BitConverter.ToString(bytes).Replace("-", "").ToLower());
        }

        /// <summary>
        /// Generates a one-way hash of a given file.
        /// 
        /// This implementation uses SHA256.
        /// </summary>
        /// <param name="filePath">File to hash</param>
        /// <returns>SHA256 hash of file content</returns>
        public Task<string> HashFile(string filePath)
        {
            using (FileStream stream = File.OpenRead(filePath))
            {
                var bytes = SHA256.Create().ComputeHash(stream);
                return Task.FromResult(BitConverter.ToString(bytes).Replace("-", "").ToLower());
            }
        }

        /// <summary>
        /// Sign data with a key and get the signature.
        /// 
        /// This implementation uses RSA.Create(), SHA512, and PKCS#1.
        /// </summary>
        /// <param name="key">Signing key</param>
        /// <param name="dataToSign">Data to sign</param>
        /// <returns>Base64-encoded signature</returns>
        public Task<string> Sign(byte[] key, string dataToSign)
        {
            using (RSA rsa = RSA.Create())
            {
                rsa.ImportRSAPrivateKey(key, out _);
                return Task.FromResult(Convert.ToBase64String(
                    rsa.SignData(
                        Encoding.Unicode.GetBytes(dataToSign),
                        HashAlgorithmName.SHA512,
                        RSASignaturePadding.Pkcs1)));
            }
        }

        /// <summary>
        /// Verify data with a given signature and key
        /// 
        /// This implementation uses RSA.Create(), SHA512, and PKCS#1.
        /// </summary>
        /// <param name="key">Signing key</param>
        /// <param name="dataToVerify">Data to verify</param>
        /// <param name="signature">Base64-encoded signature</param>
        /// <returns><c>TRUE</c> if the signature is valid</returns>
        public Task<bool> Verify(byte[] key, string dataToVerify, string signature)
        {
            using (RSA rsa = RSA.Create())
            {
                rsa.ImportRSAPublicKey(key, out _);
                return Task.FromResult(
                    rsa.VerifyData(
                        Encoding.Unicode.GetBytes(dataToVerify),
                        Convert.FromBase64String(signature),
                        HashAlgorithmName.SHA512,
                        RSASignaturePadding.Pkcs1));
            }
        }

        /// <summary>
        /// Decrypts a given cipherText with a provided key.
        /// 
        /// This implementation uses RSA.Create() and OAEP/SHA512.
        /// </summary>
        /// <param name="key">Encryption key</param>
        /// <param name="cipherText">Ciphertext (base64)</param>
        /// <returns>Decrypted string</returns>
        public Task<string> Decrypt(byte[] key, string cipherText)
        {
            using (RSA rsa = RSA.Create())
            {
                rsa.ImportRSAPrivateKey(key, out _);
                return Task.FromResult(Encoding.Unicode.GetString(
                    rsa.Decrypt(
                        Convert.FromBase64String(cipherText),
                        RSAEncryptionPadding.OaepSHA512)));
            }
        }

        /// <summary>
        /// Decrypts a given cipherText with a provided salt.
        /// 
        /// This implementation uses Aes.Create() and Rfc2898DeriveBytes.
        /// </summary>
        /// <param name="salt">Encryption salt</param>
        /// <param name="cipherText">Ciphertext (base64)</param>
        /// <returns>Decrypted string</returns>
        public async Task<string> Decrypt(string salt, string cipherText)
        {
            using (MemoryStream ms = new MemoryStream(Convert.FromBase64String(cipherText)))
            using (Aes aes = Aes.Create())
            {
                aes.Key = new Rfc2898DeriveBytes(Configuration.MasterEncryptionKey, Encoding.UTF8.GetBytes(salt)).GetBytes(128 / 8);
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

        /// <summary>
        /// Encrypts a given cipherText with a provided key.
        /// 
        /// This implementation uses RSA.Create() and OAEP/SHA512.
        /// </summary>
        /// <param name="key">Encryption key</param>
        /// <param name="plainText">Text to encrypt</param>
        /// <returns>Encrypted ciphertext</returns>
        public Task<string> Encrypt(byte[] key, string plainText)
        {
            using (RSA rsa = RSA.Create())
            {
                rsa.ImportRSAPublicKey(key, out _);
                return Task.FromResult(Convert.ToBase64String(
                    rsa.Encrypt(
                        Encoding.Unicode.GetBytes(plainText),
                        RSAEncryptionPadding.OaepSHA512)));
            }
        }

        /// <summary>
        /// Encrypts a given cipherText with a provided salt.
        /// 
        /// This implementation uses Aes.Create() and Rfc2898DeriveBytes.
        /// </summary>
        /// <param name="salt">Encryption salt</param>
        /// <param name="plainText">Plain text to encrypt</param>
        /// <returns>Encrypted ciphertext (base64)</returns>
        public async Task<string> Encrypt(string salt, string plainText)
        {
            using (MemoryStream ms = new MemoryStream())
            using (Aes aes = Aes.Create())
            {
                aes.Key = new Rfc2898DeriveBytes(Configuration.MasterEncryptionKey, Encoding.UTF8.GetBytes(salt)).GetBytes(128 / 8);
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
