// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Azure.Core;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Rest;
using Newtonsoft.Json;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AuthJanitor.Providers
{
    public class AccessTokenCredential
    {
        public string Username { get; set; }

        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }

        public TimeSpan ExpiresInTimeSpan => TimeSpan.FromSeconds(ExpiresIn);

        [JsonProperty("expires_on")]
        public long ExpiresOn { get; set; }

        public DateTimeOffset ExpiresOnDateTime
        {
            get => DateTimeOffset.FromUnixTimeSeconds(ExpiresOn);
            set => ExpiresOn = value.ToUnixTimeSeconds();
        }

        [JsonProperty("ext_expires_in")]
        public int ExtExpiresIn { get; set; }

        public TimeSpan ExtExpiresInTimeSpan
        {
            get => TimeSpan.FromSeconds(ExtExpiresIn);
            set => ExtExpiresIn = (int)value.TotalSeconds;
        }

        [JsonProperty("not_before")]
        public long NotBefore { get; set; }

        public DateTimeOffset NotBeforeDateTime
        {
            get => DateTimeOffset.FromUnixTimeSeconds(NotBefore);
            set => NotBefore = value.ToUnixTimeSeconds();
        }

        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; }

        [JsonProperty("resource")]
        public string Resource { get; set; }

        [JsonProperty("scope")]
        public string Scope { get; set; }

        [JsonProperty("token_type")]
        public string TokenType { get; set; }

        public AzureCredentials CreateAzureCredentials() =>
            new AzureCredentials(
                new TokenCredentials(this.AccessToken, this.TokenType),
                new TokenCredentials(this.AccessToken, this.TokenType),
                Environment.GetEnvironmentVariable("TENANT_ID", EnvironmentVariableTarget.Process),
                AzureEnvironment.AzureGlobalCloud);

        public TokenCredential CreateTokenCredential() =>
            new ExistingTokenCredential(this.AccessToken, this.ExpiresOnDateTime);

        public class ExistingTokenCredential : TokenCredential
        {
            private AccessToken _accessToken;
            public ExistingTokenCredential(string accessToken, DateTimeOffset expiresOn)
            {
                _accessToken = new AccessToken(accessToken, expiresOn);
            }

            public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                return _accessToken;
            }

            public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                return new ValueTask<AccessToken>(_accessToken);
            }
        }
    }
}
