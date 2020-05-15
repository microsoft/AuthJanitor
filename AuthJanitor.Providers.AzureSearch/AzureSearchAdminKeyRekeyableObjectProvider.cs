// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Extensions.Azure;
using Microsoft.Azure.Management.Search.Fluent;
using Microsoft.Azure.Management.Search.Fluent.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.AzureSearch
{
    [Provider(Name = "Azure Search Admin Key",
              IconClass = "fas fa-search",
              Description = "Regenerates an Admin Key for an Azure Search service")]
    [ProviderImage(ProviderImages.AZURE_SEARCH_SVG)]
    public class AzureSearchAdminKeyRekeyableObjectProvider : RekeyableObjectProvider<AzureSearchAdminKeyConfiguration>
    {
        private readonly ILogger _logger;

        public AzureSearchAdminKeyRekeyableObjectProvider(ILogger<AzureSearchAdminKeyRekeyableObjectProvider> logger)
        {
            _logger = logger;
        }

        public override async Task<RegeneratedSecret> GetSecretToUseDuringRekeying()
        {
            _logger.LogInformation("Getting temporary secret to use during rekeying from other ({0}) key...", GetOtherKeyKind(Configuration.KeyKind));
            var searchService = await SearchService;
            IAdminKeys keys = await searchService.GetAdminKeysAsync();
            _logger.LogInformation("Successfully retrieved temporary secret!");
            return new RegeneratedSecret()
            {
                Expiry = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(10),
                UserHint = Configuration.UserHint,
                NewSecretValue = GetKeyValue(keys, GetOtherKeyKind(Configuration.KeyKind))
            };
        }

        public override async Task<RegeneratedSecret> Rekey(TimeSpan requestedValidPeriod)
        {
            _logger.LogInformation("Regenerating Azure Search {0} admin key", Configuration.KeyKind);
            var searchService = await SearchService;
            await searchService.RegenerateAdminKeysAsync(GetKeyKind(Configuration.KeyKind));

            IAdminKeys keys = await searchService.GetAdminKeysAsync();
            _logger.LogInformation("Successfully rekeyed CosmosDB key kind {0}", Configuration.KeyKind);
            return new RegeneratedSecret()
            {
                Expiry = DateTimeOffset.UtcNow + requestedValidPeriod,
                UserHint = Configuration.UserHint,
                NewSecretValue = GetKeyValue(keys, GetKeyKind(Configuration.KeyKind))
            };
        }

        public override async Task OnConsumingApplicationSwapped()
        {
            if (!Configuration.SkipScramblingOtherKey)
            {
                _logger.LogInformation("Scrambling Azure Search key kind {0}", GetOtherKeyKind(Configuration.KeyKind));
                await (await SearchService).RegenerateAdminKeysAsync(GetOtherKeyKind(Configuration.KeyKind));
            }
            else
                _logger.LogInformation("Skipping scrambling Azure Search key kind {0}", GetOtherKeyKind(Configuration.KeyKind));
        }

        public override IList<RiskyConfigurationItem> GetRisks()
        {
            List<RiskyConfigurationItem> issues = new List<RiskyConfigurationItem>();
            if (Configuration.SkipScramblingOtherKey)
            {
                issues.Add(new RiskyConfigurationItem()
                {
                    Score = 80,
                    Risk = $"The other (unused) Azure Search Administrative Key is not being scrambled during key rotation",
                    Recommendation = "Unless other services use the alternate key, consider allowing the scrambling of the unused key to 'fully' rekey Azure Search and maintain a high degree of security."
                });
            }

            return issues;
        }

        public override string GetDescription() =>
            $"Regenerates the {Configuration.KeyKind} key for an Azure Search service " +
            $"called '{ResourceName}' (Resource Group '{ResourceGroup}'). " +
            $"The {GetOtherKeyKind(Configuration.KeyKind)} key is used as a temporary " +
            $"key while rekeying is taking place. The {GetOtherKeyKind(Configuration.KeyKind)} " +
            $"key will {(Configuration.SkipScramblingOtherKey ? "not" : "also")} be rotated.";

        private Task<ISearchService> SearchService => this.GetAzure().ContinueWith(az => az.Result.SearchServices.GetByResourceGroupAsync(ResourceGroup, ResourceName)).Unwrap();

        private AdminKeyKind GetKeyKind(AzureSearchAdminKeyConfiguration.AzureSearchKeyKinds keyKind) => keyKind switch
        {
            AzureSearchAdminKeyConfiguration.AzureSearchKeyKinds.Primary => AdminKeyKind.Primary,
            AzureSearchAdminKeyConfiguration.AzureSearchKeyKinds.Secondary => AdminKeyKind.Secondary,
            _ => throw new System.Exception($"Key kind '{keyKind}' not implemented")
        };

        private AdminKeyKind GetOtherKeyKind(AzureSearchAdminKeyConfiguration.AzureSearchKeyKinds keyKind) => keyKind switch
        {
            AzureSearchAdminKeyConfiguration.AzureSearchKeyKinds.Primary => AdminKeyKind.Secondary,
            AzureSearchAdminKeyConfiguration.AzureSearchKeyKinds.Secondary => AdminKeyKind.Primary,
            _ => throw new System.Exception($"Key kind '{keyKind}' not implemented")
        };

        private string GetKeyValue(IAdminKeys keys, AdminKeyKind keyKind) => keyKind switch
        {
            AdminKeyKind.Primary => keys.PrimaryKey,
            AdminKeyKind.Secondary => keys.SecondaryKey,
            _ => throw new System.Exception($"Key kind '{keyKind}' not implemented")
        };
    }
}
