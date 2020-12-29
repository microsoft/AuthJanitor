// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Integrations.CryptographicImplementations;
using AuthJanitor.IdentityServices;
using AuthJanitor.SecureStorage;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Options;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text;
using System.Buffers.Text;

namespace AuthJanitor.Integrations.SecureStorage.AzureKeyVault
{
    public class KeyVaultSecureStorageProvider : ISecureStorage
    {
        private KeyVaultSecureStorageProviderConfiguration Configuration { get; }
        private readonly IIdentityService _identityService;
        private readonly ICryptographicImplementation _cryptographicImplementation;

        public KeyVaultSecureStorageProvider(
            IOptions<KeyVaultSecureStorageProviderConfiguration> configuration,
            IIdentityService identityService,
            ICryptographicImplementation cryptographicImplementation)
        {
            Configuration = configuration.Value;
            _identityService = identityService;
            _cryptographicImplementation = cryptographicImplementation;

            if (_cryptographicImplementation == null)
                throw new InvalidOperationException("ICryptographicImplementation must be registered!");
        }

        public async Task Destroy(Guid persistenceId)
        {
            var client = await GetClient();
            await client.StartDeleteSecretAsync($"{Configuration.Prefix}{persistenceId}");
        }

        public async Task<Guid> Persist<T>(DateTimeOffset expiry, T persistedObject)
        {
            var newId = Guid.NewGuid();
            var newSecret = new KeyVaultSecret($"{Configuration.Prefix}{newId}",
                Convert.ToBase64String(
                await _cryptographicImplementation.Encrypt(
                    Encoding.UTF8.GetBytes(JsonSerializer.Serialize(persistedObject)))));
            newSecret.Properties.ExpiresOn = expiry;

            var client = await GetClient();
            await client.SetSecretAsync(newSecret);
            return newId;
        }

        public async Task<T> Retrieve<T>(Guid persistenceId)
        {
            var client = await GetClient();
            var secret = await client.GetSecretAsync($"{Configuration.Prefix}{persistenceId}");
            if (secret == null || secret.Value == null)
                throw new Exception("Secret not found");

            return JsonSerializer.Deserialize<T>(await _cryptographicImplementation.Decrypt(
                Convert.FromBase64String(secret.Value.Value)));
        }

        private Task<SecretClient> GetClient() =>
            _identityService.GetAccessTokenForApplicationAsync()
                .ContinueWith(t => new SecretClient(
                    new Uri($"https://{Configuration.VaultName}.vault.azure.net/"),
                    ExistingTokenCredential.FromAccessToken(t.Result)));
    }
}