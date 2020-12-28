// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.IdentityServices;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.Extensions.Options;
using System;
using System.Security;
using System.Threading.Tasks;

namespace AuthJanitor.Integrations.CryptographicImplementations.AzureKeyVault
{
    /// <summary>
    /// A remote-focused cryptographic implementation powered by Azure Key Vault.
    /// Sign/Verify/Encrypt/Decrypt operations are performed by Key Vault.
    /// Hash/Generate operations fall back to the implementation given in configuration.
    /// </summary>
    public class AzureKeyVaultCryptographicImplementation : ICryptographicImplementation
    {
        private AzureKeyVaultCryptographicImplementationConfiguration Configuration { get; }
        private ICryptographicImplementation FallbackCryptography => Configuration.FallbackCryptography;

        private readonly IIdentityService _identityService;

        /// <summary>
        /// The default AuthJanitor cryptographic implementation
        /// </summary>
        public AzureKeyVaultCryptographicImplementation(
            IOptions<AzureKeyVaultCryptographicImplementationConfiguration> configuration,
            IIdentityService identityService)
        {
            Configuration = configuration.Value;
            _identityService = identityService;
        }

        /// <summary>
        /// Generates a cryptographically random SecureString of a given length.
        /// 
        /// This implementation leverages the FallbackCryptography.
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        public Task<SecureString> GenerateCryptographicallyRandomSecureString(int length) =>
            FallbackCryptography.GenerateCryptographicallyRandomSecureString(length);

        /// <summary>
        /// Generates a cryptographically random string of a given length.
        /// 
        /// This implementation leverages the FallbackCryptography.
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        public Task<string> GenerateCryptographicallyRandomString(int length) =>
            FallbackCryptography.GenerateCryptographicallyRandomString(length);

        /// <summary>
        /// Generates a one-way hash of a given string.
        /// 
        /// This implementation leverages the FallbackCryptography.
        /// </summary>
        /// <param name="str">String to hash</param>
        /// <returns>SHA256 hash of string</returns>
        public Task<byte[]> Hash(string str) =>
            FallbackCryptography.Hash(str);

        /// <summary>
        /// Generates a one-way hash of a given byte array.
        /// 
        /// This implementation leverages the FallbackCryptography.
        /// </summary>
        /// <param name="inputBytes">Bytes to hash</param>
        /// <returns>SHA256 hash of byte array</returns>
        public Task<byte[]> Hash(byte[] inputBytes) =>
            FallbackCryptography.Hash(inputBytes);

        /// <summary>
        /// Generates a one-way hash of a given file.
        /// 
        /// This implementation leverages the FallbackCryptography.
        /// </summary>
        /// <param name="filePath">File to hash</param>
        /// <returns>SHA256 hash of file content</returns>
        public Task<byte[]> HashFile(string filePath) =>
            FallbackCryptography.HashFile(filePath);

        /// <summary>
        /// Sign data with a key and get the signature.
        /// </summary>
        /// <param name="digest">Data to sign</param>
        /// <returns>Base64-encoded signature</returns>
        public async Task<byte[]> Sign(byte[] digest) =>
            (await (await GetClient()).SignAsync(
                SignatureAlgorithm.PS512, // todo: configurable
                digest)).Signature;

        /// <summary>
        /// Verify data with a given signature and key
        /// </summary>
        /// <param name="digest">Data to verify</param>
        /// <param name="signature">Base64-encoded signature</param>
        /// <returns><c>TRUE</c> if the signature is valid</returns>
        public async Task<bool> Verify(byte[] digest, byte[] signature) =>
            (await (await GetClient()).VerifyAsync(
                SignatureAlgorithm.PS512, // todo: configurable
                digest,
                signature)).IsValid;

        /// <summary>
        /// Sign data with a key and get the signature.
        /// </summary>
        /// <param name="key">Key to use</param>
        /// <param name="digest">Data to sign</param>
        /// <returns>Base64-encoded signature</returns>
        public async Task<byte[]> Sign(string key, byte[] digest) =>
            (await (await GetClient(key)).SignAsync(
                SignatureAlgorithm.PS512, // todo: configurable
                digest)).Signature;

        /// <summary>
        /// Verify data with a given signature and key
        /// </summary>
        /// <param name="key">Key to use</param>
        /// <param name="digest">Data to verify</param>
        /// <param name="signature">Base64-encoded signature</param>
        /// <returns><c>TRUE</c> if the signature is valid</returns>
        public async Task<bool> Verify(string key, byte[] digest, byte[] signature) =>
            (await (await GetClient(key)).VerifyAsync(
                SignatureAlgorithm.PS512, // todo: configurable
                digest,
                signature)).IsValid;

        /// <summary>
        /// Decrypts a given cipherText with a provided key.
        /// </summary>
        /// <param name="cipherText">Ciphertext (base64)</param>
        /// <returns>Decrypted string</returns>
        public async Task<byte[]> Decrypt(byte[] cipherText) =>
            (await (await GetClient()).DecryptAsync(
                EncryptionAlgorithm.Rsa15,
                cipherText)).Plaintext;

        /// <summary>
        /// Encrypts a given cipherText with a provided key.
        /// </summary>
        /// <param name="plainText">Text to encrypt</param>
        /// <returns>Encrypted ciphertext</returns>
        public async Task<byte[]> Encrypt(byte[] plainText) =>
            (await (await GetClient()).EncryptAsync(
                EncryptionAlgorithm.Rsa15,
                plainText)).Ciphertext;

        /// <summary>
        /// Decrypts a given cipherText with a provided key.
        /// </summary>
        /// <param name="key">Key to use</param>
        /// <param name="cipherText">Ciphertext (base64)</param>
        /// <returns>Decrypted string</returns>
        public async Task<byte[]> Decrypt(string key, byte[] cipherText) =>
            (await (await GetClient(key)).DecryptAsync(
                EncryptionAlgorithm.Rsa15,
                cipherText)).Plaintext;

        /// <summary>
        /// Encrypts a given cipherText with a provided key.
        /// </summary>
        /// <param name="key">Key to use</param>
        /// <param name="plainText">Text to encrypt</param>
        /// <returns>Encrypted ciphertext</returns>
        public async Task<byte[]> Encrypt(string key, byte[] plainText) =>
            (await (await GetClient(key)).EncryptAsync(
                EncryptionAlgorithm.Rsa15,
                plainText)).Ciphertext;

        private Task<CryptographyClient> GetClient() =>
            _identityService.GetAccessTokenForApplicationAsync()
                .ContinueWith(t => new CryptographyClient(
                    new Uri(Configuration.KeyIdUri),
                    ExistingTokenCredential.FromAccessToken(t.Result)));

        private Task<CryptographyClient> GetClient(string key) =>
            _identityService.GetAccessTokenForApplicationAsync()
                    .ContinueWith(async t =>
                    {
                        var keyClient = new KeyClient(new Uri(Configuration.VaultUri),
                            ExistingTokenCredential.FromAccessToken(t.Result));
                        var keyObject = await keyClient.GetKeyAsync(key);

                        return new CryptographyClient(
                            keyObject.Value.Id,
                            ExistingTokenCredential.FromAccessToken(t.Result));
                    }).Unwrap();
    }
}
