// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Automation.Shared;
using Azure.Security.KeyVault.Secrets;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace AuthJanitor.Automation.SecureStorageProviders.AzureKeyVault
{
    public class KeyVaultSecureStorageProvider : ISecureStorageProvider
    {
        private const string PERSISTENCE_PREFIX = "AJPersist-";
        private readonly string _vaultName;

        private readonly IPersistenceEncryption _encryption;
        private readonly CredentialProviderService _credentialProvider;
        public KeyVaultSecureStorageProvider(
            IPersistenceEncryption encryption,
            CredentialProviderService credentialProvider,
            string vaultName)
        {
            _encryption = encryption;
            if (_encryption == null) throw new InvalidOperationException("You must register IPersistenceEncryption, even if using no encryption!");
            _credentialProvider = credentialProvider;
            _vaultName = vaultName;
        }

        public async Task Destroy(Guid persistenceId)
        {
            await GetClient().StartDeleteSecretAsync($"{PERSISTENCE_PREFIX}{persistenceId}");
        }

        public async Task<Guid> Persist<T>(DateTimeOffset expiry, T persistedObject)
        {
            var newId = Guid.NewGuid();
            var newSecret = new KeyVaultSecret($"{PERSISTENCE_PREFIX}{newId}",
                await _encryption.Encrypt(
                    newId.ToString(),
                    JsonConvert.SerializeObject(persistedObject)));
            newSecret.Properties.ExpiresOn = expiry;

            await GetClient().SetSecretAsync(newSecret);
            return newId;
        }

        public async Task<T> Retrieve<T>(Guid persistenceId)
        {
            var secret = await GetClient().GetSecretAsync($"{PERSISTENCE_PREFIX}{persistenceId}");
            if (secret == null || secret.Value == null)
                throw new Exception("Secret not found");

            return JsonConvert.DeserializeObject<T>(
                await _encryption.Decrypt(persistenceId.ToString(), secret.Value.Value));
        }

        private SecretClient GetClient()
        {
            return new SecretClient(new Uri($"https://{_vaultName}.vault.azure.net/"),
                _credentialProvider.GetAgentIdentity().CreateTokenCredential());
        }
    }
}
