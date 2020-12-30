// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.CryptographicImplementations;
using AuthJanitor.Integrations.CryptographicImplementations;
using AuthJanitor.Integrations.CryptographicImplementations.Default;
using Microsoft.Extensions.Options;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace AuthJanitor.Tests
{
    public class CryptographicTests
    {
        [Fact]
        public async Task ValidatesRandomStringLengthIsCorrect()
        {
            var expectedLength = 24;

            var notary = GetCryptoNotary();
            var secureString = await notary.GenerateCryptographicallyRandomString(expectedLength);
            Assert.Equal(expectedLength, secureString.Length);
        }

        [Fact]
        public async Task ValidatesRandomSecureStringLengthIsCorrect()
        {
            var expectedLength = 24;

            var notary = GetCryptoNotary();
            var secureString = await notary.GenerateCryptographicallyRandomSecureString(expectedLength);
            Assert.Equal(expectedLength, secureString.Length);
        }

        [Fact]
        public async Task ValidatesRandomStringsDiffer()
        {
            var expectedLength = 24;

            var notary = GetCryptoNotary();
            var secureStringA = await notary.GenerateCryptographicallyRandomString(expectedLength);
            var secureStringB = await notary.GenerateCryptographicallyRandomString(expectedLength);
            Assert.NotEqual(secureStringA, secureStringB);
        }

        [Fact]
        public async Task ValidatesRandomSecureStringsDiffer()
        {
            var expectedLength = 24;

            var notary = GetCryptoNotary();
            var secureStringA = await notary.GenerateCryptographicallyRandomSecureString(expectedLength);
            var secureStringB = await notary.GenerateCryptographicallyRandomSecureString(expectedLength);
            Assert.NotEqual(secureStringA, secureStringB);
        }

        [Fact]
        public async Task RoundtripEncryptionIsSuccessful()
        {
            var secret = Encoding.ASCII.GetBytes("MySecret");

            var notary = GetCryptoNotary();
            var cipherText = await notary.Encrypt(secret);
            var clearText = await notary.Decrypt(cipherText);
            Assert.Equal(secret, clearText);
        }

        [Fact]
        public async Task ValidatesHashFunctionUsesSHA256()
        {
            var hashInput = "This is my value";
            var expectedHash = "cb23f1d0b2b1f605366d6fc1da2ed25f445a6bd2975c1ada270c578628a3a820";

            var notary = GetCryptoNotary();
            var hashedInput = BitConverter.ToString(await notary.Hash(hashInput)).Replace("-","").ToLower();
            Assert.Equal(expectedHash, hashedInput);
        }

        private ICryptographicImplementation GetCryptoNotary()
        {
            var rsa = RSA.Create();
            var publicKey = rsa.ExportRSAPublicKey();
            var privateKey = rsa.ExportRSAPrivateKey();
            var config = new DefaultCryptographicImplementationConfiguration()
            {
                PublicKey = publicKey,
                PrivateKey = privateKey
            };
            return new DefaultCryptographicImplementation(Options.Create(config));
        }
    }
}
