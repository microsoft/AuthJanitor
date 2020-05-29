using AuthJanitor.Integrations.CryptographicImplementations;
using AuthJanitor.Integrations.CryptographicImplementations.Default;
using Microsoft.Extensions.Options;
using System;
using System.Data.SqlTypes;
using System.Threading.Tasks;
using Xunit;

namespace AuthJanitor.Tests
{
    public class CryptographicTests
    {
        [Fact]
        public async Task RoundtripEncryptionIsSuccessful()
        {
            var salt = "RoundtripEncryptionIsSuccessful";
            var secret = "MySecret";

            var notary = GetCryptoNotary();
            var cipherText = await notary.Encrypt(salt, secret);
            var clearText = await notary.Decrypt(salt, cipherText);
            Assert.Equal(secret, clearText);
        }

        [Fact]
        public async Task EnsureReasonableEntropyExists()
        {
            var salt = "RoundtripEncryptionIsSuccessful";
            var secret = "MySecret";

            var notary = GetCryptoNotary();
            var cipherA =  await notary.Encrypt(salt, secret);
            var cipherB = await notary.Encrypt(salt, secret);
            Assert.NotEqual(cipherA, cipherB);
        }

        [Fact]
        public async Task ValidatesHashFunctionUsesSHA256()
        {
            var hashInput = "This is my value";
            var expectedHash = "cb23f1d0b2b1f605366d6fc1da2ed25f445a6bd2975c1ada270c578628a3a820";

            var notary = GetCryptoNotary();
            var hashedInput = await notary.Hash(hashInput);
            Assert.Equal(expectedHash, hashedInput);
        }

        private ICryptographicImplementation GetCryptoNotary()
        {
            var config = new DefaultCryptographicImplementationConfiguration()
            {
                MasterEncryptionKey = "This is my master key!"
            };
            return new DefaultCryptographicImplementation(Options.Create(config));
        }
    }
}
