// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Extensions.Azure;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.KeyVault
{
    [Provider(Name = "Key Vault Secret",
              IconClass = "fa fa-low-vision",
              Description = "Manages the lifecycle of a Key Vault Secret where a Managed Secret's value is stored")]
    [ProviderImage(ProviderImages.KEY_VAULT_SVG)]
    public class KeyVaultSecretApplicationLifecycleProvider : ApplicationLifecycleProvider<KeyVaultSecretLifecycleConfiguration>
    {
        /// <summary>
        /// Logger implementation
        /// </summary>
        protected ILogger Logger { get; }

        public KeyVaultSecretApplicationLifecycleProvider(ILogger<KeyVaultSecretApplicationLifecycleProvider> logger)
        {
            Logger = logger;
        }

        /// <summary>
        /// Call to commit the newly generated secret
        /// </summary>
        public override async Task CommitNewSecrets(List<RegeneratedSecret> newSecrets)
        {
            Logger.LogInformation("Committing new secrets to Key Vault secret {0}", Configuration.SecretName);
            var client = GetSecretClient();
            foreach (RegeneratedSecret secret in newSecrets)
            {
                Logger.LogInformation("Getting current secret version from secret name {0}", Configuration.SecretName);
                Azure.Response<KeyVaultSecret> currentSecret = await client.GetSecretAsync(Configuration.SecretName);

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

                Logger.LogInformation("Committing new secret '{0}'", secretName);
                await client.SetSecretAsync(newKvSecret);
                Logger.LogInformation("Successfully committed new secret '{0}'", secretName);
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
