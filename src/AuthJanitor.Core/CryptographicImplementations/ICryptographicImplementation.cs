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
        /// Decrypt sensitive data
        /// </summary>
        /// <param name="salt">Encryption salt</param>
        /// <param name="cipherText">Encrypted ciphertext</param>
        /// <returns>Decrypted text</returns>
        Task<string> Decrypt(string salt, string cipherText);

        /// <summary>
        /// Encrypt sensitive data
        /// </summary>
        /// <param name="salt">Encryption salt</param>
        /// <param name="plainText">Text to encrypt</param>
        /// <returns>Encrypted ciphertext</returns>
        Task<string> Encrypt(string salt, string plainText);
    }
}
