// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers.Azure;
using Azure;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.KeyVault
{
    [Provider(Name = "Key Vault Secret",
              IconClass = "fa fa-low-vision",
              Description = "Manages the lifecycle of a Key Vault Secret where a Managed Secret's value is stored",
              Features = ProviderFeatureFlags.IsTestable)]
    [ProviderImage(ProviderImages.KEY_VAULT_SVG)]
    public class KeyVaultSecretApplicationLifecycleProvider : ApplicationLifecycleProvider<KeyVaultSecretLifecycleConfiguration>
    {
        private readonly ILogger _logger;

        public KeyVaultSecretApplicationLifecycleProvider(ILogger<KeyVaultSecretApplicationLifecycleProvider> logger)
        {
            _logger = logger;
        }

        public override async Task Test()
        {
            var secret = await GetSecretClient().GetSecretAsync(Configuration.SecretName);
            if (secret == null) throw new Exception("Could not access Key Vault Secret");
        }

        /// <summary>
        /// Call to commit the newly generated secret
        /// </summary>
        public override async Task CommitNewSecrets(List<RegeneratedSecret> newSecrets)
        {
            _logger.LogInformation("Committing new secrets to Key Vault secret {SecretName}", Configuration.SecretName);
            var client = GetSecretClient();
            foreach (RegeneratedSecret secret in newSecrets)
            {
                _logger.LogInformation("Getting current secret version from secret name {SecretName}", Configuration.SecretName);
                Response<KeyVaultSecret> currentSecret = await client.GetSecretAsync(Configuration.SecretName);

                // Create a new version of the Secret
                string secretName = string.IsNullOrEmpty(secret.UserHint) ? Configuration.SecretName : $"{Configuration.SecretName}-{secret.UserHint}";
                KeyVaultSecret newKvSecret = new KeyVaultSecret(secretName, secret.NewSecretValue);

                // Copy in metadata from the old Secret if it existed
                if (currentSecret != null && currentSecret.Value != null)
                {
                    newKvSecret.Properties.ContentType = currentSecret.Value.Properties.ContentType;
                    foreach (KeyValuePair<string, string> tag in currentSecret.Value.Properties.Tags)
                    {
                        newKvSecret.Properties.Tags.Add(tag.Key, tag.Value);
                    }
                    newKvSecret.Properties.Tags.Add("UserHint", secret.UserHint);
                }

                newKvSecret.Properties.NotBefore = DateTimeOffset.UtcNow;
                newKvSecret.Properties.ExpiresOn = secret.Expiry;

                _logger.LogInformation("Committing new secret '{SecretName}'", secretName);
                await client.SetSecretAsync(newKvSecret);
                _logger.LogInformation("Successfully committed new secret '{SecretName}'", secretName);
            }
        }

        private SecretClient GetSecretClient() =>
            new SecretClient(new Uri($"https://{Configuration.VaultName}.vault.azure.net/"),
                Credential.CreateTokenCredential());

        public override string GetDescription() =>
            $"Populates a Key Vault Secret called '{Configuration.SecretName}' " +
            $"from vault '{Configuration.VaultName}' with a given " +
            (Configuration.CommitAsConnectionString ? "connection string" : "key") + ".";
    }
}
