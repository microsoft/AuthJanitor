// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.CryptographicImplementations;
using AuthJanitor.Providers.Azure;
using Azure;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.KeyVault
{
    [Provider(Name = "Key Vault Secret",
              IconClass = "fa fa-low-vision",
              Description = "Regenerates a Key Vault Secret with a given length",
              Features = ProviderFeatureFlags.CanRotateWithoutDowntime |
                         ProviderFeatureFlags.IsTestable |
                         ProviderFeatureFlags.SupportsSecondaryKey)]
    [ProviderImage(ProviderImages.KEY_VAULT_SVG)]
    public class KeyVaultSecretRekeyableObjectProvider : RekeyableObjectProvider<KeyVaultSecretConfiguration>
    {
        private readonly ICryptographicImplementation _cryptographicImplementation;
        private readonly ILogger _logger;

        public KeyVaultSecretRekeyableObjectProvider(
            ILogger<KeyVaultSecretRekeyableObjectProvider> logger,
            ICryptographicImplementation cryptographicImplementation)
        {
            _logger = logger;
            _cryptographicImplementation = cryptographicImplementation;
        }

        public override async Task Test()
        {
            var secret = await GetSecretClient().GetSecretAsync(Configuration.SecretName);
            if (secret == null) throw new Exception("Could not access Key Vault Secret");
        }

        public override async Task<RegeneratedSecret> GetSecretToUseDuringRekeying()
        {
            _logger.LogInformation("Getting temporary secret based on current version...");
            var client = GetSecretClient();
            Response<KeyVaultSecret> currentSecret = await client.GetSecretAsync(Configuration.SecretName);
            _logger.LogInformation("Successfully retrieved temporary secret!");
            return new RegeneratedSecret()
            {
                Expiry = currentSecret.Value.Properties.ExpiresOn.Value,
                UserHint = Configuration.UserHint,
                NewSecretValue = currentSecret.Value.Value
            };
        }

        public override async Task<RegeneratedSecret> Rekey(TimeSpan requestedValidPeriod)
        {
            _logger.LogInformation("Getting current Secret details from Secret name '{SecretName}'", Configuration.SecretName);
            var client = GetSecretClient();
            Response<KeyVaultSecret> currentSecret = await client.GetSecretAsync(Configuration.SecretName);

            // Create a new version of the Secret
            KeyVaultSecret newSecret = new KeyVaultSecret(
                Configuration.SecretName,
                await _cryptographicImplementation.GenerateCryptographicallySecureString(Configuration.SecretLength));

            // Copy in metadata from the old Secret if it existed
            if (currentSecret != null && currentSecret.Value != null)
            {
                newSecret.Properties.ContentType = currentSecret.Value.Properties.ContentType;
                foreach (KeyValuePair<string, string> tag in currentSecret.Value.Properties.Tags)
                {
                    newSecret.Properties.Tags.Add(tag.Key, tag.Value);
                }
            }

            newSecret.Properties.NotBefore = DateTimeOffset.UtcNow;
            newSecret.Properties.ExpiresOn = DateTimeOffset.UtcNow + requestedValidPeriod;

            _logger.LogInformation("Committing new Secret with name '{SecretName}'", newSecret.Name);
            Response<KeyVaultSecret> secretResponse = await client.SetSecretAsync(newSecret);
            _logger.LogInformation("Successfully committed '{SecretName}'", newSecret.Name);

            return new RegeneratedSecret()
            {
                Expiry = newSecret.Properties.ExpiresOn.Value,
                UserHint = Configuration.UserHint,
                NewSecretValue = secretResponse.Value.Value
            };
        }
        public override IList<RiskyConfigurationItem> GetRisks()
        {
            List<RiskyConfigurationItem> issues = new List<RiskyConfigurationItem>();
            if (Configuration.SecretLength < 16)
            {
                issues.Add(new RiskyConfigurationItem()
                {
                    Score = 80,
                    Risk = $"The specified secret length is extremely short ({Configuration.SecretLength} characters), making it easier to compromise through brute force attacks",
                    Recommendation = "Increase the length of the secret to over 32 characters; prefer 64 or up."
                });
            }
            else if (Configuration.SecretLength < 32)
            {
                issues.Add(new RiskyConfigurationItem()
                {
                    Score = 40,
                    Risk = $"The specified secret length is somewhat short ({Configuration.SecretLength} characters), making it easier to compromise through brute force attacks",
                    Recommendation = "Increase the length of the secret to over 32 characters; prefer 64 or up."
                });
            }
            return issues;
        }

        public override IList<RiskyConfigurationItem> GetRisks(TimeSpan requestedValidPeriod)
        {
            List<RiskyConfigurationItem> issues = new List<RiskyConfigurationItem>();
            if (requestedValidPeriod == TimeSpan.MaxValue)
            {
                issues.Add(new RiskyConfigurationItem()
                {
                    Score = 80,
                    Risk = $"The specified Valid Period is TimeSpan.MaxValue, which is effectively Infinity; it is dangerous to allow infinite periods of validity because it allows an object's prior version to be available after the object has been rotated",
                    Recommendation = "Specify a reasonable value for Valid Period"
                });
            }
            else if (requestedValidPeriod == TimeSpan.Zero)
            {
                issues.Add(new RiskyConfigurationItem()
                {
                    Score = 100,
                    Risk = $"The specified Valid Period is zero, so this object will never be allowed to be used",
                    Recommendation = "Specify a reasonable value for Valid Period"
                });
            }
            return issues.Union(GetRisks()).ToList();
        }

        public override string GetDescription() =>
            $"Regenerates the secret called '{Configuration.SecretName}' from vault " +
            $"'{Configuration.VaultName}' with a length of {Configuration.SecretLength}.";

        private SecretClient GetSecretClient() =>
            new SecretClient(new Uri($"https://{Configuration.VaultName}.vault.azure.net/"),
                Credential.CreateTokenCredential());
    }
}
