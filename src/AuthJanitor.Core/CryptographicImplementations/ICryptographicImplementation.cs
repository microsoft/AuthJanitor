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
        Task<byte[]> Hash(string str);

        /// <summary>
        /// Hash a given array of bytes
        /// </summary>
        /// <param name="bytes">Input bytes</param>
        /// <returns>One-way hash of input bytes</returns>
        Task<byte[]> Hash(byte[] bytes);

        /// <summary>
        /// Hash the contents of a given file
        /// </summary>
        /// <param name="filePath">File to hash</param>
        /// <returns>One-way hash of file content</returns>
        Task<byte[]> HashFile(string filePath);

        /// <summary>
        /// Sign data and get the signature
        /// </summary>
        /// <param name="digest">Data to sign</param>
        /// <returns>Signature</returns>
        Task<byte[]> Sign(byte[] digest);

        /// <summary>
        /// Verify data with a given signature
        /// </summary>
        /// <param name="digest">Data to verify</param>
        /// <param name="signature">Signature</param>
        /// <returns><c>TRUE</c> if the signature is valid</returns>
        Task<bool> Verify(byte[] digest, byte[] signature);

        /// <summary>
        /// Sign data with a key and get the signature
        /// </summary>
        /// <param name="key">Key to use</param>
        /// <param name="digest">Data to sign</param>
        /// <returns>Signature</returns>
        Task<byte[]> Sign(string key, byte[] digest);

        /// <summary>
        /// Verify data with a given signature and key
        /// </summary>
        /// <param name="key">Key to use</param>
        /// <param name="digest">Data to verify</param>
        /// <param name="signature">Signature</param>
        /// <returns><c>TRUE</c> if the signature is valid</returns>
        Task<bool> Verify(string key, byte[] digest, byte[] signature);

        /// <summary>
        /// Decrypt sensitive data
        /// </summary>
        /// <param name="cipherText">Encrypted ciphertext</param>
        /// <returns>Decrypted text</returns>
        Task<byte[]> Decrypt(byte[] cipherText);

        /// <summary>
        /// Encrypt sensitive data
        /// </summary>
        /// <param name="plainText">Text to encrypt</param>
        /// <returns>Encrypted ciphertext</returns>
        Task<byte[]> Encrypt(byte[] plainText);

        /// <summary>
        /// Decrypt sensitive data
        /// </summary>
        /// <param name="key">Key to use</param>
        /// <param name="cipherText">Encrypted ciphertext</param>
        /// <returns>Decrypted text</returns>
        Task<byte[]> Decrypt(string key, byte[] cipherText);

        /// <summary>
        /// Encrypt sensitive data
        /// </summary>
        /// <param name="key">Key to use</param>
        /// <param name="plainText">Text to encrypt</param>
        /// <returns>Encrypted ciphertext</returns>
        Task<byte[]> Encrypt(string key, byte[] plainText);
    }
}
