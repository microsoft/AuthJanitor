// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.Threading.Tasks;

namespace AuthJanitor.Automation.Shared
{
    public interface IPersistenceEncryption
    {
        /// <summary>
        /// Encrypt sensitive data
        /// </summary>
        /// <param name="salt">Encryption salt</param>
        /// <param name="plainText">Text to encrypt</param>
        /// <returns>Encrypted ciphertext</returns>
        Task<string> Encrypt(string salt, string plainText);

        /// <summary>
        /// Decrypt sensitive data
        /// </summary>
        /// <param name="salt">Encryption salt</param>
        /// <param name="cipherText">Encrypted ciphertext</param>
        /// <returns>Decrypted text</returns>
        Task<string> Decrypt(string salt, string cipherText);
    }

    public class NoPersistenceEncryption : IPersistenceEncryption
    {
        public Task<string> Decrypt(string salt, string cipherText) => Task.FromResult(cipherText);

        public Task<string> Encrypt(string salt, string plainText) => Task.FromResult(plainText);
    }
}
