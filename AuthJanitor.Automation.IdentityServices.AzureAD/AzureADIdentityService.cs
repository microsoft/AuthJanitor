// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Automation.Shared;
using AuthJanitor.Providers;
using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AuthJanitor.Automation.IdentityServices.AzureAD
{
    /// <summary>
    /// Implements an identity service which reads from the HttpContext to integrate with Azure Active Directory
    /// </summary>
    public class AzureADIdentityService : IIdentityService
    {
        public const string AGENT_CREDENTIAL_USERNAME = "application.identity@local";
        public const string DEFAULT_OBO_RESOURCE = "https://management.core.windows.net";

        private const string ROLES_CLAIM = "roles";
        private const string ID_TOKEN_OBO_HEADER_NAME = "X-MS-TOKEN-AAD-ID-TOKEN";

        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// Implements an identity service which reads from the HttpContext to integrate with Azure Active Directory
        /// </summary>
        public AzureADIdentityService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

#if DEBUG
        private static bool IsRunningLocally => string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));
#endif

        /// <summary>
        /// Return if there is currently a user logged in (with any valid AuthJanitor role)
        /// </summary>
        public bool IsUserLoggedIn =>
#if DEBUG
            IsRunningLocally ? true :
#endif
            _httpContextAccessor.HttpContext != null &&
            _httpContextAccessor.HttpContext.User != null &&
            _httpContextAccessor.HttpContext.User.Claims != null &&
            GetClaimsInternal(ROLES_CLAIM).Any(r => AuthJanitorRoles.ALL_ROLES.Contains(r));

        /// <summary>
        /// Return the current user's name
        /// </summary>
        public string UserName =>
            IsUserLoggedIn ? $"{GetClaimInternal(ClaimTypes.GivenName)} {GetClaimInternal(ClaimTypes.Surname)}"
            : string.Empty;

        /// <summary>
        /// Return the current user's e-mail address
        /// </summary>
        public string UserEmail =>
            IsUserLoggedIn ? GetClaimInternal(ClaimTypes.Email)
            : string.Empty;

        /// <summary>
        /// Return a list of the current user's roles
        /// </summary>
        public string[] UserRoles =>
#if DEBUG
            IsRunningLocally ? new string[] { AuthJanitorRoles.GlobalAdmin } :
#endif
            IsUserLoggedIn ? GetClaimsInternal(ROLES_CLAIM).ToArray()
                           : new string[0];

        /// <summary>
        /// If the currently logged in user has the given role (or is a GlobalAdmin)
        /// </summary>
        /// <param name="authJanitorRole">Role to test</param>
        public bool CurrentUserHasRole(string authJanitorRole) =>
#if DEBUG
            IsRunningLocally ? true :
#endif
            IsUserLoggedIn ? GetClaimsInternal(ROLES_CLAIM).Any(r => r.Equals(authJanitorRole) || r.Equals(AuthJanitorRoles.GlobalAdmin))
                           : false;

        /// <summary>
        /// Retrieve the Access Token for the current underlying MSI/SP identity
        /// </summary>
        /// <param name="scopes">Scopes to request with Access Token</param>
        /// <returns>Access Token</returns>
        public Task<AccessTokenCredential> GetAccessTokenForApplicationAsync(params string[] scopes)
        {
            return new DefaultAzureCredential(new DefaultAzureCredentialOptions()
            {
                ExcludeEnvironmentCredential = false,
                ExcludeInteractiveBrowserCredential = true,
                ExcludeManagedIdentityCredential = false,
                ExcludeSharedTokenCacheCredential = true
            }).GetTokenAsync(new TokenRequestContext(scopes))
              .AsTask()
              .ContinueWith(t => new AccessTokenCredential()
              {
                  Username = AGENT_CREDENTIAL_USERNAME,
                  AccessToken = t.Result.Token,
                  ExpiresOnDateTime = t.Result.ExpiresOn
              });
        }

        /// <summary>
        /// Retrieve an Access Token for a resource on behalf of the current user
        /// </summary>
        /// <param name="resource">Resource to perform on-behalf-of exchange for</param>
        /// <returns>Access Token</returns>
        public async Task<AccessTokenCredential> GetAccessTokenOnBehalfOfCurrentUserAsync(string resource = DEFAULT_OBO_RESOURCE)
        {
            if (!_httpContextAccessor.HttpContext.Request.Headers.ContainsKey(ID_TOKEN_OBO_HEADER_NAME))
                throw new InvalidOperationException($"HttpContext Request Headers does not contain '{ID_TOKEN_OBO_HEADER_NAME}'");

            var idToken = _httpContextAccessor.HttpContext.Request.Headers[ID_TOKEN_OBO_HEADER_NAME].FirstOrDefault();
            if (string.IsNullOrEmpty(idToken))
                throw new InvalidOperationException($"HttpContext Request Header '{ID_TOKEN_OBO_HEADER_NAME}' does not contain a value");

            var dict = new Dictionary<string, string>()
            {
                { "grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer" },
                { "client_id", Environment.GetEnvironmentVariable("CLIENT_ID", EnvironmentVariableTarget.Process) },
                { "client_secret", Environment.GetEnvironmentVariable("CLIENT_SECRET", EnvironmentVariableTarget.Process) },
                { "assertion", idToken },
                { "resource", resource },
                { "requested_token_use", "on_behalf_of" }
            };

            var result = await new HttpClient().PostAsync("https://login.microsoftonline.com/" +
                Environment.GetEnvironmentVariable("TENANT_ID", EnvironmentVariableTarget.Process) +
                "/oauth2/token",
                new FormUrlEncodedContent(dict));
            var tokenResponse = JsonConvert.DeserializeObject<AccessTokenCredential>(
                await result.Content.ReadAsStringAsync());

            return tokenResponse;
        }

        private IEnumerable<string> GetClaimsInternal(string claimType) =>
#if DEBUG
            IsRunningLocally ? new string[] { "#DEBUG#" } :
#endif
            (_httpContextAccessor.HttpContext == null ||
             _httpContextAccessor.HttpContext.User == null ||
             _httpContextAccessor.HttpContext.User.Claims == null) ? new string[0] :

            _httpContextAccessor.HttpContext.User.Claims
                        .Where(c => c.Type == claimType)
                        .Select(c => c.Value)
                        .Distinct()
                        .ToArray();

        private string GetClaimInternal(string claimType) => GetClaimsInternal(claimType).FirstOrDefault();
    }
}
