// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.CryptographicImplementations;
using AuthJanitor.Providers.Azure;
using AuthJanitor.Providers.Capabilities;
using Azure;
using Azure.Security.KeyVault.Keys;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.KeyVault
{
    [Provider(Name = "Key Vault Key",
              Description = "Regenerates an Azure Key Vault Key with the same parameters as the previous version",
              SvgImage = ProviderImages.KEY_VAULT_SVG)]
    public class KeyVaultKeyRekeyableObjectProvider : 
        AuthJanitorProvider<KeyVaultKeyConfiguration>,
        ICanRekey,
        ICanRunSanityTests,
        ICanGenerateTemporarySecretValue
    {
        public KeyVaultKeyRekeyableObjectProvider(ProviderWorkflowActionLogger<KeyVaultKeyRekeyableObjectProvider> logger) : base(logger) { }

        public async Task Test()
        {
            var key = await GetKeyClient().GetKeyAsync(Configuration.KeyName);
            if (key == null) throw new Exception("Key was not found or not accessible");
        }

        public async Task<RegeneratedSecret> GenerateTemporarySecretValue()
        {
            Logger.LogInformation("Getting temporary secret to use during rekeying based on current KID");
            var client = GetKeyClient();
            Response<KeyVaultKey> currentKey = await client.GetKeyAsync(Configuration.KeyName);
            Logger.LogInformation("Successfully retrieved temporary secret!");

            return new RegeneratedSecret()
            {
                UserHint = Configuration.UserHint,
                NewSecretValue = currentKey.Value.Key.Id.GetSecureString()
            };
        }

        public async Task<RegeneratedSecret> Rekey(TimeSpan requestedValidPeriod)
        {
            Logger.LogInformation("Regenerating Key Vault key {KeyName}", Configuration.KeyName);
            var client = GetKeyClient();
            Response<KeyVaultKey> currentKey = await client.GetKeyAsync(Configuration.KeyName);

            CreateKeyOptions creationOptions = new CreateKeyOptions()
            {
                Enabled = true,
                ExpiresOn = DateTimeOffset.UtcNow + requestedValidPeriod,
                NotBefore = DateTimeOffset.UtcNow
            };
            foreach (KeyOperation op in currentKey.Value.KeyOperations)
            {
                creationOptions.KeyOperations.Add(op);
            }

            foreach (System.Collections.Generic.KeyValuePair<string, string> tag in currentKey.Value.Properties.Tags)
            {
                creationOptions.Tags.Add(tag.Key, tag.Value);
            }

            Response<KeyVaultKey> key = await client.CreateKeyAsync(Configuration.KeyName, currentKey.Value.KeyType, creationOptions);
            Logger.LogInformation("Successfully rekeyed Key Vault key {KeyName}", Configuration.KeyName);

            return new RegeneratedSecret()
            {
                UserHint = Configuration.UserHint,
                NewSecretValue = key.Value.Key.Id.GetSecureString()
            };
        }

        public override IList<RiskyConfigurationItem> GetRisks(TimeSpan requestedValidPeriod)
        {
            List<RiskyConfigurationItem> issues = new List<RiskyConfigurationItem>();
            if (requestedValidPeriod == TimeSpan.MaxValue)
            {
                issues.Add(new RiskyConfigurationItem()
                {
                    Score = 80,
                    Risk = $"The specificed Valid Period is TimeSpan.MaxValue, which is effectively Infinity; it is dangerous to allow infinite periods of validity because it allows an object's prior version to be available after the object has been rotated",
                    Recommendation = "Specify a reasonable value for Valid Period"
                });
            }
            else if (requestedValidPeriod == TimeSpan.Zero)
            {
                issues.Add(new RiskyConfigurationItem()
                {
                    Score = 100,
                    Risk = $"The specificed Valid Period is zero, so this object will never be allowed to be used",
                    Recommendation = "Specify a reasonable value for Valid Period"
                });
            }
            return issues.Union(base.GetRisks(requestedValidPeriod)).ToList();
        }

        public override string GetDescription() =>
            $"Regenerates the key called '{Configuration.KeyName}' from vault " +
            $"'{Configuration.VaultName}' with its current parameters and at its " +
            $"current key size.";

        private KeyClient GetKeyClient() =>
            new KeyClient(new Uri($"https://{Configuration.VaultName}.vault.azure.net/"),
                Credential.CreateTokenCredential());
    }
}
