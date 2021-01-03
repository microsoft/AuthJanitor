// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace AuthJanitor.IdentityServices
{
    public static class DummyIdentityServiceExtensions
    {
        public static void AddDummyIdentityService(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IIdentityService, DummyIdentityService>();
        }
    }

    public class DummyIdentityService : IIdentityService
    {
        public bool IsUserLoggedIn => true;

        public string UserEmail => "dummy@_local_";

        public string UserName => "Local User";

        public string[] UserRoles => AuthJanitorRoles.ALL_ROLES;

        public bool CurrentUserHasRole(string authJanitorRole) => true;

        public Task<AccessTokenCredential> GetAccessTokenForApplicationAsync(params string[] scopes) =>
            Task.FromResult(new AccessTokenCredential());

        public Task<AccessTokenCredential> GetAccessTokenOnBehalfOfCurrentUserAsync(string resource = "https://management.core.windows.net") =>
            Task.FromResult(new AccessTokenCredential());
    }
}
