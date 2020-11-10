// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Integrations.CryptographicImplementations;
using AuthJanitor.Providers.Azure;
using AuthJanitor.Providers.Capabilities;
using Azure.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.AzureAD
{
    [Provider(Name = "Access Token",
              Description = "Acquires an Access Token from Azure AD with a given set of scopes",
              SvgImage = ProviderImages.AZURE_AD_SVG)]
    public class AccessTokenRekeyableObjectProvider : 
        AuthJanitorProvider<AccessTokenConfiguration>,
        ICanRekey
    {
        private readonly ILogger _logger;

        public AccessTokenRekeyableObjectProvider(ILogger<AccessTokenRekeyableObjectProvider> logger)
        {
            _logger = logger;
        }

        public async Task<RegeneratedSecret> Rekey(TimeSpan requestedValidPeriod)
        {
            // TODO: If we use admin-approved, we need to bubble scopes up to the original token request
            // ..... or re-request the Bearer token on approval, which might be ugly.
            // TODO: How to refresh this?

            // NOTE: requestedValidPeriod is ignored here, AAD sets token expiry!
            _logger.LogInformation("Requesting Access Token with scopes '{RequestedScopes}'", Configuration.Scopes);
            var token = await Credential.CreateTokenCredential()
                .GetTokenAsync(new TokenRequestContext(Configuration.Scopes), System.Threading.CancellationToken.None);

            _logger.LogInformation("Access Token successfully granted! Expires on {TokenExpiresOn}", token.ExpiresOn);
            return new RegeneratedSecret()
            {
                UserHint = Configuration.UserHint,
                NewSecretValue = token.Token.GetSecureString(),
                Expiry = token.ExpiresOn
            };
        }

        /// <summary>
        /// Get a list of configuration choices that might be risky
        /// </summary>
        /// <returns></returns>
        public override IList<RiskyConfigurationItem> GetRisks()
        {
            List<RiskyConfigurationItem> issues = new List<RiskyConfigurationItem>();
            if (Configuration.Scopes.Length > 10)
            {
                issues.Add(new RiskyConfigurationItem()
                {
                    Score = 70,
                    Risk = $"There are more than 10 ({Configuration.Scopes.Length}) scopes defined for a single access token",
                    Recommendation = "Reduce the number of scopes per token by segregating access between data security boundaries in the application(s)."
                });
            }

            return issues;
        }

        public override string GetDescription() =>
            $"Requests an Access Token with the scopes {string.Join(", ", Configuration.Scopes)}.";
    }
}
