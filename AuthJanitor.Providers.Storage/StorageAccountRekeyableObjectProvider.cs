// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Microsoft.Azure.Management.Storage.Fluent;
using Microsoft.Azure.Management.Storage.Fluent.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.Storage
{
    [Provider(Name = "Storage Account Key",
              IconClass = "fas fa-file-alt",
              Description = "Regenerates a key of a specified type for an Azure Storage Account")]
    [ProviderImage(ProviderImages.STORAGE_ACCOUNT_SVG)]
    public class StorageAccountRekeyableObjectProvider : RekeyableObjectProvider<StorageAccountKeyConfiguration>
    {
        private const string KEY1 = "key1";
        private const string KEY2 = "key2";
        private const string KERB1 = "kerb1";
        private const string KERB2 = "kerb2";

        public StorageAccountRekeyableObjectProvider(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        public override async Task<RegeneratedSecret> GetSecretToUseDuringRekeying()
        {
            Logger.LogInformation("Getting temporary secret to use during rekeying from other ({0}) key...", OtherKeyName);
            StorageAccountKey newKey = await Get(OtherKeyName);
            Logger.LogInformation("Successfully retrieved temporary secret!");
            return new RegeneratedSecret()
            {
                Expiry = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(10),
                UserHint = Configuration.UserHint,
                NewSecretValue = newKey?.Value,
                NewConnectionString = $"DefaultEndpointsProtocol=https;AccountName={ResourceName};AccountKey={newKey?.Value};EndpointSuffix=core.windows.net"
            };
        }

        public override async Task<RegeneratedSecret> Rekey(TimeSpan requestedValidPeriod)
        {
            Logger.LogInformation("Regenerating Storage key type {0}", Configuration.KeyType);
            StorageAccountKey newKey = await Regenerate(KeyName);
            Logger.LogInformation("Successfully rekeyed Storage key type {0}", Configuration.KeyType);
            return new RegeneratedSecret()
            {
                Expiry = DateTimeOffset.UtcNow + requestedValidPeriod,
                UserHint = Configuration.UserHint,
                NewSecretValue = newKey?.Value,
                NewConnectionString = $"DefaultEndpointsProtocol=https;AccountName={ResourceName};AccountKey={newKey?.Value};EndpointSuffix=core.windows.net"
            };
        }

        public override async Task OnConsumingApplicationSwapped()
        {
            if (!Configuration.SkipScramblingOtherKey)
            {
                Logger.LogInformation("Scrambling Storage key kind {0}", OtherKeyName);
                await Regenerate(OtherKeyName);
            }
            else
                Logger.LogInformation("Skipping scrambling Storage key kind {0}", OtherKeyName);
        }

        public override IList<RiskyConfigurationItem> GetRisks()
        {
            List<RiskyConfigurationItem> issues = new List<RiskyConfigurationItem>();
            if (Configuration.SkipScramblingOtherKey)
            {
                issues.Add(new RiskyConfigurationItem()
                {
                    Score = 80,
                    Risk = $"The other (unused) Storage Account Key of this type is not being scrambled during key rotation",
                    Recommendation = "Unless other services use the alternate key, consider allowing the scrambling of the unused key to 'fully' rekey the Storage Account and maintain a high degree of security."
                });
            }

            return issues;
        }

        public override string GetDescription() =>
            $"Regenerates the {KeyName} key for a Storage Account called " +
            $"'{ResourceName}' (Resource Group '{ResourceGroup}'). " +
            $"The {OtherKeyName} key is used as a temporary key while " +
            $"rekeying is taking place. The {OtherKeyName} key will " +
            $"{(Configuration.SkipScramblingOtherKey ? "not" : "also")} be rotated.";

        private async Task<StorageAccountKey> Regenerate(string keyName) =>
            await (await StorageAccount).RegenerateKeyAsync(keyName)
                                        .ContinueWith(keys => keys.Result.FirstOrDefault(k => k.KeyName == keyName));
        private async Task<StorageAccountKey> Get(string keyName) =>
            await (await StorageAccount).GetKeysAsync()
                                        .ContinueWith(keys => keys.Result.FirstOrDefault(k => k.KeyName == keyName));

        private Task<IStorageAccount> StorageAccount => GetAzure().ContinueWith(az => az.Result.StorageAccounts.GetByResourceGroupAsync(ResourceGroup, ResourceName)).Unwrap();

        private string KeyName => Configuration.KeyType switch
        {
            StorageAccountKeyConfiguration.StorageKeyTypes.Key1 => KEY1,
            StorageAccountKeyConfiguration.StorageKeyTypes.Key2 => KEY2,
            StorageAccountKeyConfiguration.StorageKeyTypes.Kerb1 => KERB1,
            StorageAccountKeyConfiguration.StorageKeyTypes.Kerb2 => KERB2,
            _ => throw new Exception($"KeyType '{Configuration.KeyType}' not implemented")
        };
        private string OtherKeyName => Configuration.KeyType switch
        {
            StorageAccountKeyConfiguration.StorageKeyTypes.Key1 => KEY2,
            StorageAccountKeyConfiguration.StorageKeyTypes.Key2 => KEY1,
            StorageAccountKeyConfiguration.StorageKeyTypes.Kerb1 => KERB2,
            StorageAccountKeyConfiguration.StorageKeyTypes.Kerb2 => KERB1,
            _ => throw new Exception($"KeyType '{Configuration.KeyType}' not implemented")
        };
    }
}
