// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.Threading.Tasks;

namespace AuthJanitor.IdentityServices
{
    public interface IIdentityService
    {
        /// <summary>
        /// Return if there is currently a user logged in (with any valid AuthJanitor role)
        /// </summary>
        bool IsUserLoggedIn { get; }

        /// <summary>
        /// Return the current user's e-mail address
        /// </summary>
        string UserEmail { get; }

        /// <summary>
        /// Return the current user's name
        /// </summary>
        string UserName { get; }

        /// <summary>
        /// Return a list of the current user's roles
        /// </summary>
        string[] UserRoles { get; }

        /// <summary>
        /// If the currently logged in user has the given role (or is a GlobalAdmin)
        /// </summary>
        /// <param name="authJanitorRole">Role to test</param>
        bool CurrentUserHasRole(string authJanitorRole);

        /// <summary>
        /// Retrieve the Access Token for the current underlying MSI/SP identity
        /// </summary>
        /// <param name="scopes">Scopes to request with Access Token</param>
        /// <returns>Access Token</returns>
        Task<AccessTokenCredential> GetAccessTokenForApplicationAsync(params string[] scopes);

        /// <summary>
        /// Retrieve an Access Token for a resource on behalf of the current user
        /// </summary>
        /// <param name="resource">Resource to perform on-behalf-of exchange for</param>
        /// <returns>Access Token</returns>
        Task<AccessTokenCredential> GetAccessTokenOnBehalfOfCurrentUserAsync(string resource = "https://management.core.windows.net");
    }
}
