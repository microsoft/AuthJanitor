// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.Security;
using System.Threading.Tasks;

namespace AuthJanitor.Integrations.CryptographicImplementations
{
    public interface ICryptographicImplementation : IAuthJanitorExtensibilityPoint
    {
        /// <summary>
        /// Generate a new cryptographically random SecureString with a given length
        /// </summary>
        /// <param name="length">Length to generate</param>
        /// <returns>Generated SecureString</returns>
        Task<SecureString> GenerateCryptographicallyRandomSecureString(int length);

        /// <summary>
        /// Generate a new cryptographically random string with a given length
        /// </summary>
        /// <param name="length">Length to generate</param>
        /// <returns>Generated string</returns>
        Task<string> GenerateCryptographicallyRandomString(int length);

        /// <summary>
        /// Hash a given string
        /// </summary>
        /// <param name="str">Input string</param>
        /// <returns>One-way hash of input string</returns>
        Task<string> Hash(string str);

        /// <summary>
        /// Hash a given array of bytes
        /// </summary>
        /// <param name="bytes">Input bytes</param>
        /// <returns>One-way hash of input bytes</returns>
        Task<string> Hash(byte[] bytes);

        /// <summary>
        /// Hash the contents of a given file
        /// </summary>
        /// <param name="filePath">File to hash</param>
        /// <returns>One-way hash of file content</returns>
        Task<string> HashFile(string filePath);

        /// <summary>
        /// Sign data with a key and get the signature
        /// </summary>
        /// <param name="key">Signing key</param>
        /// <param name="dataToSign">Data to sign</param>
        /// <returns>Base64-encoded signature</returns>
        Task<string> Sign(byte[] key, string dataToSign);

        /// <summary>
        /// Verify data with a given signature and key
        /// </summary>
        /// <param name="key">Signing key</param>
        /// <param name="dataToVerify">Data to verify</param>
        /// <param name="signature">Base64-encoded signature</param>
        /// <returns><c>TRUE</c> if the signature is valid</returns>
        Task<bool> Verify(byte[] key, string dataToVerify, string signature);

        /// <summary>
        /// Decrypt sensitive data
        /// </summary>
        /// <param name="key">Encryption key</param>
        /// <param name="cipherText">Encrypted ciphertext</param>
        /// <returns>Decrypted text</returns>
        Task<string> Decrypt(byte[] key, string cipherText);

        /// <summary>
        /// Decrypt sensitive data
        /// </summary>
        /// <param name="salt">Encryption salt</param>
        /// <param name="cipherText">Encrypted ciphertext</param>
        /// <returns>Decrypted text</returns>
        Task<string> Decrypt(string salt, string cipherText);

        /// <summary>
        /// Encrypt sensitive data
        /// </summary>
        /// <param name="key">Encryption key</param>
        /// <param name="plainText">Text to encrypt</param>
        /// <returns>Encrypted ciphertext</returns>
        Task<string> Encrypt(byte[] key, string plainText);

        /// <summary>
        /// Encrypt sensitive data
        /// </summary>
        /// <param name="salt">Encryption salt</param>
        /// <param name="plainText">Text to encrypt</param>
        /// <returns>Encrypted ciphertext</returns>
        Task<string> Encrypt(string salt, string plainText);
    }
}
