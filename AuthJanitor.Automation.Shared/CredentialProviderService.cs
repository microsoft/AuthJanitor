// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Automation.Shared.Models;
using AuthJanitor.Providers;
using Azure.Identity;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AuthJanitor.Automation.Shared
{
    /// <summary>
    /// The CredentialProviderService abstracts access to credentials for the current user, a cached user, or the app's identity.
    /// 
    /// This service expects that an ISecureStorageProvider has been registered.
    /// </summary>
    public class CredentialProviderService
    {
        public const string AGENT_CREDENTIAL_USERNAME = "application.identity@local";
        private const string ID_TOKEN_OBO_HEADER_NAME = "X-MS-TOKEN-AAD-ID-TOKEN";
        private const string DEFAULT_OBO_RESOURCE = "https://management.core.windows.net";

        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ISecureStorageProvider _secureStorageProvider;

        /// <summary>
        /// The CredentialProviderService abstracts access to credentials for the current user, a cached user, or the app's identity.
        /// 
        /// This service expects that an ISecureStorageProvider has been registered if Cached Identities are used.
        /// If an ISecureStorageProvider has not been registered and any caching functions are called, a NotSupportedException will be thrown.
        /// </summary>
        public CredentialProviderService(
            ISecureStorageProvider secureStorageProvider,
            IHttpContextAccessor httpContextAccessor)
        {
            _secureStorageProvider = secureStorageProvider;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Get the Access Token required to fulfill a given RekeyingTask
        /// </summary>
        /// <param name="task">RekeyingTask to fulfill</param>
        /// <returns>Access Token</returns>
        public Task<AccessTokenCredential> GetAccessTokenAsync(RekeyingTask task)
        {
            if (task.ConfirmationType == TaskConfirmationStrategies.AdminCachesSignOff)
            {
                if (task.PersistedCredentialId == default)
                    throw new KeyNotFoundException("Cached sign-off is preferred but no credentials were persisted!");

                if (_secureStorageProvider == null)
                    throw new NotSupportedException("Must register an ISecureStorageProvider");

                return _secureStorageProvider.Retrieve<AccessTokenCredential>(task.PersistedCredentialId);
            }
            else if (task.ConfirmationType == TaskConfirmationStrategies.AdminSignsOffJustInTime)
            {
                return GetAccessTokenOnBehalfOfCurrentUserAsync();
            }
            else if (task.ConfirmationType.UsesServicePrincipal())
            {
                return GetAccessTokenForApplicationAsync();
            }
            throw new NotSupportedException("No Access Tokens could be generated for this Task!");
        }

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
            }).GetTokenAsync(new Azure.Core.TokenRequestContext(scopes))
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

        /// <summary>
        /// Cache an Access Token for a resource on behalf of the current user for future use
        /// </summary>
        /// <param name="expiry">Expiry of cache item</param>
        /// <param name="resource">Resource to request a token for</param>
        /// <returns>Guid of cached object</returns>
        public Task<Guid> CacheAccessTokenOnBehalfOfCurrentUserAsync(DateTimeOffset expiry, string resource = DEFAULT_OBO_RESOURCE)
        {
            if (_secureStorageProvider == null)
                throw new NotSupportedException("Must register an ISecureStorageProvider");
            if (expiry < DateTimeOffset.UtcNow)
                throw new ArgumentException("Expiry time is before present time");

            return GetAccessTokenOnBehalfOfCurrentUserAsync(resource)
                .ContinueWith(t => _secureStorageProvider.Persist(expiry, t.Result))
                .Unwrap();
        }

        /// <summary>
        /// Return the current user's name
        /// </summary>
        /// <returns>Logged in user's GivenName+Surname</returns>
        public string GetCurrentUserName()
        {
            if (_httpContextAccessor.HttpContext == null ||
                _httpContextAccessor.HttpContext.User == null)
                return string.Empty;

            var claims = _httpContextAccessor.HttpContext.User.Claims;
            return claims.FirstOrDefault(c => c.Type == ClaimTypes.GivenName)?.Value +
                   " " +
                   claims.FirstOrDefault(c => c.Type == ClaimTypes.Surname)?.Value;
        }

        /// <summary>
        /// Return the current user's e-mail address
        /// </summary>
        /// <returns>Logged in user's e-mail address</returns>
        public string GetCurrentUserEmail()
        {
            if (_httpContextAccessor.HttpContext == null ||
                _httpContextAccessor.HttpContext.User == null)
                return string.Empty;

            var claims = _httpContextAccessor.HttpContext.User.Claims;
            return claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        }

        /// <summary>
        /// Retrieve an Access Token from cache given its cached object ID
        /// </summary>
        /// <param name="cachedObjectId">Cached Object ID</param>
        /// <returns>Cached Access Token</returns>
        public Task<AccessTokenCredential> GetAccessTokenFromCacheAsync(Guid cachedObjectId)
        {
            if (_secureStorageProvider == null)
                throw new NotSupportedException("Must register an ISecureStorageProvider");
            if (cachedObjectId == Guid.Empty)
                throw new ArgumentException("Invalid cached object ID");

            return _secureStorageProvider.Retrieve<AccessTokenCredential>(cachedObjectId);
        }

        /// <summary>
        /// Destroy an Access Token and remove it from cache given its cached object ID
        /// </summary>
        /// <param name="cachedObjectId">Cached Object ID</param>
        public Task DestroyCachedAccessTokenAsync(Guid cachedObjectId)
        {
            if (_secureStorageProvider == null)
                throw new NotSupportedException("Must register an ISecureStorageProvider");
            if (cachedObjectId == Guid.Empty)
                throw new ArgumentException("Invalid cached object ID");

            return _secureStorageProvider.Destroy(cachedObjectId);
        }
    }
}