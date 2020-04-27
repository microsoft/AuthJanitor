// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers;
using Azure.Identity;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace AuthJanitor.Automation.Shared
{
    public class CredentialProviderService
    {
        private readonly ISecureStorageProvider _secureStorageProvider;
        private readonly AccessTokenCredential _agentIdentity;
        private AccessTokenCredential _userIdentity;

        public CredentialProviderService(
            ISecureStorageProvider secureStorageProvider)
        {
            _secureStorageProvider = secureStorageProvider;

            // Get local client identity
            var token = new DefaultAzureCredential(false).GetToken(new Azure.Core.TokenRequestContext());
            _agentIdentity = new AccessTokenCredential()
            {
                AccessToken = token.Token,
                ExpiresOnDateTime = token.ExpiresOn
            };
        }

        public AccessTokenCredential GetAgentIdentity() => _agentIdentity;
        public AccessTokenCredential GetUserIdentity() => _userIdentity;
        public Task<Guid> PutCachedIdentity(DateTimeOffset expiry, AccessTokenCredential AccessTokenCredential) =>
            _secureStorageProvider.Persist<AccessTokenCredential>(expiry, AccessTokenCredential);
        public Task<AccessTokenCredential> GetCachedIdentity(Guid id) =>
            _secureStorageProvider.Retrieve<AccessTokenCredential>(id);
        public Task DestroyCachedIdentity(Guid id) =>
            _secureStorageProvider.Destroy(id);

        public async Task RegisterAadIdToken(string token)
        {
            var dict = new Dictionary<string, string>()
            {
                { "grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer" },
                { "client_id", Environment.GetEnvironmentVariable("CLIENT_ID", EnvironmentVariableTarget.Process) },
                { "client_secret", Environment.GetEnvironmentVariable("CLIENT_SECRET", EnvironmentVariableTarget.Process) },
                { "assertion", token },
                { "resource", "https://management.core.windows.net" },
                { "requested_token_use", "on_behalf_of" }
            };

            var result = await new HttpClient().PostAsync("https://login.microsoftonline.com/" +
                Environment.GetEnvironmentVariable("TENANT_ID", EnvironmentVariableTarget.Process) +
                "/oauth2/token",
                new FormUrlEncodedContent(dict));
            var tokenResponse = JsonConvert.DeserializeObject<AccessTokenCredential>(
                await result.Content.ReadAsStringAsync());

            _userIdentity = tokenResponse;
        }
    }
}
