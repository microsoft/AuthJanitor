// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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
        public Task<byte[]> Hash(string str) => Hash(Encoding.UTF8.GetBytes(str));

        /// <summary>
        /// Generates a one-way hash of a given byte array.
        /// 
        /// This implementation uses SHA256.Create()
        /// </summary>
        /// <param name="inputBytes">Bytes to hash</param>
        /// <returns>SHA256 hash of byte array</returns>
        public Task<byte[]> Hash(byte[] inputBytes) =>
            Task.FromResult(SHA256.Create().ComputeHash(inputBytes));

        /// <summary>
        /// Generates a one-way hash of a given file.
        /// 
        /// This implementation uses SHA256.
        /// </summary>
        /// <param name="filePath">File to hash</param>
        /// <returns>SHA256 hash of file content</returns>
        public Task<byte[]> HashFile(string filePath)
        {
            using (FileStream stream = File.OpenRead(filePath))
            {
                return Task.FromResult(SHA256.Create().ComputeHash(stream));
            }
        }

        /// <summary>
        /// Sign data with a key and get the signature.
        /// 
        /// This implementation uses RSA.Create(), SHA512, and PKCS#1.
        /// </summary>
        /// <param name="digest">Data to sign</param>
        /// <returns>Signature</returns>
        public Task<byte[]> Sign(byte[] digest)
        {
            using (RSA rsa = GetRSAPublic(Configuration.PrivateKey))
            { 
                return Task.FromResult(
                    rsa.SignData(
                        digest,
                        HashAlgorithmName.SHA512,
                        RSASignaturePadding.Pkcs1));
            }
        }

        /// <summary>
        /// Verify data with a given signature and key
        /// 
        /// This implementation uses RSA.Create(), SHA512, and PKCS#1.
        /// </summary>
        /// <param name="dataToVerify">Data to verify</param>
        /// <param name="signature">Signature</param>
        /// <returns><c>TRUE</c> if the signature is valid</returns>
        public Task<bool> Verify(byte[] dataToVerify, byte[] signature)
        {
            using (RSA rsa = GetRSAPublic(Configuration.PublicKey))
            { 
                return Task.FromResult(
                    rsa.VerifyData(
                        dataToVerify,
                        signature,
                        HashAlgorithmName.SHA512,
                        RSASignaturePadding.Pkcs1));
            }
        }

        /// <summary>
        /// Sign data with a key and get the signature.
        /// 
        /// This implementation uses RSA.Create(), SHA512, and PKCS#1.
        /// </summary>
        /// <param name="key">Key to use</param>
        /// <param name="digest">Data to sign</param>
        /// <returns>Signature</returns>
        public Task<byte[]> Sign(string key, byte[] digest)
        {
            using (RSA rsa = GetRSAPrivate(Configuration.OtherPrivateKeys[key]))
            { 
                return Task.FromResult(
                    rsa.SignData(
                        digest,
                        HashAlgorithmName.SHA512,
                        RSASignaturePadding.Pkcs1));
            }
        }

        /// <summary>
        /// Verify data with a given signature and key
        /// 
        /// This implementation uses RSA.Create(), SHA512, and PKCS#1.
        /// </summary>
        /// <param name="key">Key to use</param>
        /// <param name="dataToVerify">Data to verify</param>
        /// <param name="signature">Signature</param>
        /// <returns><c>TRUE</c> if the signature is valid</returns>
        public Task<bool> Verify(string key, byte[] dataToVerify, byte[] signature)
        {
            using (RSA rsa = GetRSAPublic(Configuration.OtherPublicKeys[key]))
            { 
                return Task.FromResult(
                    rsa.VerifyData(
                        dataToVerify,
                        signature,
                        HashAlgorithmName.SHA512,
                        RSASignaturePadding.Pkcs1));
            }
        }

        /// <summary>
        /// Decrypts a given cipherText with a provided key.
        /// 
        /// This implementation uses RSA.Create() and OAEP/SHA512.
        /// </summary>
        /// <param name="cipherText">Ciphertext</param>
        /// <returns>Decrypted string</returns>
        public Task<byte[]> Decrypt(byte[] cipherText)
        {
            using (RSA rsa = GetRSAPrivate(Configuration.PrivateKey))
            { 
                return Task.FromResult(
                    rsa.Decrypt(cipherText, RSAEncryptionPadding.OaepSHA512));
            }
        }

        /// <summary>
        /// Encrypts a given cipherText with a provided key.
        /// 
        /// This implementation uses RSA.Create() and OAEP/SHA512.
        /// </summary>
        /// <param name="plainText">Text to encrypt</param>
        /// <returns>Encrypted ciphertext</returns>
        public Task<byte[]> Encrypt(byte[] plainText)
        {
            using (RSA rsa = GetRSAPublic(Configuration.PublicKey))
            { 
                return Task.FromResult(
                    rsa.Encrypt(plainText, RSAEncryptionPadding.OaepSHA512));
            }
        }

        /// <summary>
        /// Decrypts a given cipherText with a provided key.
        /// 
        /// This implementation uses RSA.Create() and OAEP/SHA512.
        /// </summary>
        /// <param name="key">Key to use</param>
        /// <param name="cipherText">Ciphertext</param>
        /// <returns>Decrypted string</returns>
        public Task<byte[]> Decrypt(string key, byte[] cipherText)
        {
            using (RSA rsa = GetRSAPrivate(Configuration.OtherPrivateKeys[key]))
            { 
                return Task.FromResult(
                    rsa.Decrypt(cipherText, RSAEncryptionPadding.OaepSHA512));
            }
        }

        /// <summary>
        /// Encrypts a given cipherText with a provided key.
        /// 
        /// This implementation uses RSA.Create() and OAEP/SHA512.
        /// </summary>
        /// <param name="key">Key to use</param>
        /// <param name="plainText">Text to encrypt</param>
        /// <returns>Encrypted ciphertext</returns>
        public Task<byte[]> Encrypt(string key, byte[] plainText)
        {
            using (RSA rsa = GetRSAPublic(Configuration.OtherPublicKeys[key]))
            {
                return Task.FromResult(
                    rsa.Encrypt(plainText, RSAEncryptionPadding.OaepSHA512));
            }
        }

        private RSA GetRSAPrivate(byte[] keyMaterial)
        {
            var rsa = RSA.Create();
            rsa.ImportRSAPrivateKey(keyMaterial, out _);
            return rsa;
        }

        private RSA GetRSAPublic(byte[] keyMaterial)
        {
            var rsa = RSA.Create();
            rsa.ImportRSAPublicKey(keyMaterial, out _);
            return rsa;
        }
}
}
