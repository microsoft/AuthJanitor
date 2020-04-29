// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Microsoft.Azure.Management.Maps;
using Microsoft.Azure.Management.Maps.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.AzureMaps
{
    [Provider(Name = "Azure Maps Key",
              IconClass = "fa fa-map",
              Description = "Regenerates a key for an Azure Maps instance")]
    [ProviderImage(ProviderImages.AZURE_MAPS_SVG)]
    public class AzureMapsRekeyableObjectProvider : RekeyableObjectProvider<AzureMapsConfiguration>
    {
        private const string PRIMARY_KEY = "primary";
        private const string SECONDARY_KEY = "secondary";

        public AzureMapsRekeyableObjectProvider(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        public override async Task<RegeneratedSecret> GetSecretToUseDuringRekeying()
        {
            Logger.LogInformation("Getting temporary secret to use during rekeying from other ({0}) key...", GetOtherKeyType);
            var keys = await ManagementClient.Accounts.ListKeysAsync(
                ResourceGroup,
                ResourceName);
            Logger.LogInformation("Successfully retrieved temporary secret!");
            return new RegeneratedSecret()
            {
                Expiry = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(10),
                UserHint = Configuration.UserHint,
                NewSecretValue = GetKeyValue(keys, GetOtherKeyType)
            };
        }

        public override async Task<RegeneratedSecret> Rekey(TimeSpan requestedValidPeriod)
        {
            Logger.LogInformation("Regenerating Azure Maps key type '{0}'", GetKeyType);
            var keys = await ManagementClient.Accounts.RegenerateKeysAsync(
                ResourceGroup,
                ResourceName,
                new MapsKeySpecification(GetKeyType));
            Logger.LogInformation("Successfully regenerated Azure Maps key type '{0}'", GetKeyType);
            return new RegeneratedSecret()
            {
                Expiry = DateTimeOffset.UtcNow + requestedValidPeriod,
                UserHint = Configuration.UserHint,
                NewSecretValue = GetKeyValue(keys, GetKeyType)
            };
        }

        public override async Task OnConsumingApplicationSwapped()
        {
            if (!Configuration.SkipScramblingOtherKey)
            {
                Logger.LogInformation("Scrambling Azure Maps key type '{0}'", GetOtherKeyType);
                await ManagementClient.Accounts.RegenerateKeysAsync(
                    ResourceGroup,
                    ResourceName,
                    new MapsKeySpecification(GetOtherKeyType));
            }
            else
                Logger.LogInformation("Skipping scrambling Azure Maps key type '{0}'", GetOtherKeyType);
        }

        public override IList<RiskyConfigurationItem> GetRisks()
        {
            List<RiskyConfigurationItem> issues = new List<RiskyConfigurationItem>();
            if (Configuration.SkipScramblingOtherKey)
            {
                issues.Add(new RiskyConfigurationItem()
                {
                    Score = 80,
                    Risk = $"The other (unused) Azure Maps Key is not being scrambled during key rotation",
                    Recommendation = "Unless other services use the alternate key, consider allowing the scrambling of the unused key to 'fully' rekey Azure Maps and maintain a high degree of security."
                });
            }

            return issues;
        }

        public override string GetDescription() =>
            $"Regenerates the {GetKeyType} key for an Azure Maps instance " +
            $"called '{ResourceName}' (Resource Group '{ResourceGroup}'). " +
            $"The {GetOtherKeyType} key is used as a temporary " +
            $"key while rekeying is taking place. The {GetOtherKeyType} " +
            $"key will {(Configuration.SkipScramblingOtherKey ? "not" : "also")} be rotated.";

        private MapsManagementClient ManagementClient => new MapsManagementClient(Credential.CreateAzureCredentials());

        private string GetKeyValue(MapsAccountKeys accountKeys, string keyType) => keyType switch
        {
            PRIMARY_KEY => accountKeys.PrimaryKey,
            SECONDARY_KEY => accountKeys.SecondaryKey,
            _ => throw new NotImplementedException()
        };

        private string GetKeyType => Configuration.KeyType switch
        {
            AzureMapsConfiguration.AzureMapsKeyType.Primary => PRIMARY_KEY,
            AzureMapsConfiguration.AzureMapsKeyType.Secondary => SECONDARY_KEY,
            _ => throw new NotImplementedException()
        };

        private string GetOtherKeyType => Configuration.KeyType switch
        {
            AzureMapsConfiguration.AzureMapsKeyType.Primary => SECONDARY_KEY,
            AzureMapsConfiguration.AzureMapsKeyType.Secondary => PRIMARY_KEY,
            _ => throw new NotImplementedException()
        };
    }
}
