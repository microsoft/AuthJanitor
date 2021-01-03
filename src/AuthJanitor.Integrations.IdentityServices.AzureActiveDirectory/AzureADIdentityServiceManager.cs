// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.IdentityServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace AuthJanitor.Integrations.IdentityServices.AzureActiveDirectory
{
    public class AzureADIdentityServiceManager : IIdentityServiceManager
    {
        private const string PERMISSION_USER_READBASIC_ALL = "user.readbasic.all";
        private const string PERMISSION_APP_READ_ALL = "application.read.all";
        private const string PERMISSION_APPROLE_ASSIGNMENT_READWRITE_ALL = "appRoleAssignment.readWrite.all";

        private AzureADIdentityServiceConfiguration Configuration { get; }

        private readonly ILogger<AzureADIdentityServiceManager> _logger;
        private readonly ITokenCredentialProvider _tokenCredentialProvider;

        /// <summary>
        /// Implements user management and role controls for an identity service which reads from the HttpContext to integrate with Azure Active Directory
        /// </summary>
        public AzureADIdentityServiceManager(
            ITokenCredentialProvider tokenCredentialProvider,
            IOptions<AzureADIdentityServiceConfiguration> configuration,
            ILogger<AzureADIdentityServiceManager> logger)
        {
            _tokenCredentialProvider = tokenCredentialProvider;
            Configuration = configuration.Value;
            _logger = logger;
        }

        public async Task<IList<AuthJanitorAuthorizedUser>> GetAuthorizedUsers()
        {
            var users = new List<AuthJanitorAuthorizedUser>();

            var client = await GetGraphServiceClientAsync(PERMISSION_USER_READBASIC_ALL, PERMISSION_APP_READ_ALL);

            _logger.LogInformation("Getting SP");
            var appServicePrincipal = await GetAppServicePrincipalAsync(client);

            _logger.LogInformation("Getting role assignments");
            var appRoleAssignments = await client
                    .ServicePrincipals[appServicePrincipal.Id]
                    .AppRoleAssignedTo
                    .Request()
                    .GetAsync();

            await Task.WhenAll(
                appServicePrincipal.AppRoles.Select(async role =>
                {
                    // TODO: Include Groups here too
                    var userObjectIds = appRoleAssignments
                        .Where(a => a.AppRoleId == role.Id && a.PrincipalType == "User")
                        .Select(a => a.PrincipalId.ToString())
                        .ToList();

                    await Task.WhenAll(
                        userObjectIds.Select(async id =>
                        {
                            var user = await client.Users[id].Request().GetAsync();
                            users.Add(new AuthJanitorAuthorizedUser()
                            {
                                Role = role.DisplayName,
                                RoleValue = role.Value,
                                UPN = user.UserPrincipalName,
                                DisplayName = user.DisplayName
                            });
                        }));
                }));

            return users;
        }

        public async Task AddAuthorizedUser(string userPrincipalName, string role)
        {
            var client = await GetGraphServiceClientAsync(PERMISSION_USER_READBASIC_ALL, PERMISSION_APP_READ_ALL, PERMISSION_APPROLE_ASSIGNMENT_READWRITE_ALL);

            var appServicePrincipal = await GetAppServicePrincipalAsync(client);
            var userPrincipal = await GetUserByPrincipalNameAsync(client, userPrincipalName);
            var appRole = appServicePrincipal.AppRoles.FirstOrDefault(r => r.Value.Equals(role, StringComparison.OrdinalIgnoreCase));
            if (appRole == null)
                throw new KeyNotFoundException("Role not found");

            await client
                .ServicePrincipals[appServicePrincipal.Id]
                .AppRoleAssignments
                .Request()
                .AddAsync(new AppRoleAssignment()
                {
                    AppRoleId = appRole.Id,
                    PrincipalId = Guid.Parse(userPrincipal.Id),
                    ResourceId = Guid.Parse(appServicePrincipal.Id)
                });
        }

        public async Task RemoveAuthorizedUser(string userPrincipalName)
        {
            var client = await GetGraphServiceClientAsync(PERMISSION_USER_READBASIC_ALL, PERMISSION_APP_READ_ALL, PERMISSION_APPROLE_ASSIGNMENT_READWRITE_ALL);

            var appServicePrincipal = await GetAppServicePrincipalAsync(client);
            var userPrincipal = await GetUserByPrincipalNameAsync(client, userPrincipalName);

            var appRoleAssignments = (await client
                .ServicePrincipals[appServicePrincipal.Id]
                .AppRoleAssignedTo
                .Request()
                .GetAsync()).Where(p => p.PrincipalId == Guid.Parse(userPrincipal.Id)).ToList();
            if (!appRoleAssignments.Any())
                return; // assignment does not exist, drop call

            await Task.WhenAll(appRoleAssignments.Select(async assignment =>
            {
                await client
                    .ServicePrincipals[appServicePrincipal.Id]
                    .AppRoleAssignments[assignment.Id]
                    .Request()
                    .DeleteAsync();
            }));
        }

        public async Task RemoveAuthorizedUser(string userPrincipalName, string role)
        {
            var client = await GetGraphServiceClientAsync(PERMISSION_USER_READBASIC_ALL, PERMISSION_APP_READ_ALL, PERMISSION_APPROLE_ASSIGNMENT_READWRITE_ALL);

            var appServicePrincipal = await GetAppServicePrincipalAsync(client);
            var userPrincipal = await GetUserByPrincipalNameAsync(client, userPrincipalName);
            var appRole = appServicePrincipal.AppRoles.FirstOrDefault(r => r.Value.Equals(role, StringComparison.OrdinalIgnoreCase));
            if (appRole == null)
                throw new KeyNotFoundException("Role not found");

            var appRoleAssignment = (await client
                .ServicePrincipals[appServicePrincipal.Id]
                .AppRoleAssignedTo
                .Request()
                .GetAsync()).Where(p => p.PrincipalId == Guid.Parse(userPrincipal.Id) && p.AppRoleId == appRole.Id).FirstOrDefault();
            if (appRoleAssignment == null)
                return; // assignment does not exist, drop call

            await client
                .ServicePrincipals[appServicePrincipal.Id]
                .AppRoleAssignments[appRoleAssignment.Id]
                .Request()
                .DeleteAsync();
        }

        private async Task<User> GetUserByPrincipalNameAsync(GraphServiceClient client, string userPrincipalName) => (await client
                .Users[userPrincipalName]
                .Request()
                //.Filter($"userPrincipalName eq '{userPrincipalName}'")
                .GetAsync());//.FirstOrDefault();

        private async Task<ServicePrincipal> GetAppServicePrincipalAsync(GraphServiceClient client) => (await client
            .ServicePrincipals
            .Request()
            .Filter($"appId eq '{Configuration.ClientId}'")
            .GetAsync()).FirstOrDefault();

        private async Task<GraphServiceClient> GetGraphServiceClientAsync(params string[] scopes)
        {
            var token = await _tokenCredentialProvider.GetToken(Configuration.TokenSource, string.Empty);

            if (!token.IsInError)
                _logger.LogInformation("Got token for user {user} -- resource {resource} -- scope {scope}", token.Username, token.Resource, token.Scope);
            else
                throw new AccessViolationException($"Token exchange failed! Error: {token.ErrorType} ({token.SubErrorType}) ... Code(s): {string.Join(", ", token.ErrorCodes)}");

            var graphServiceClient = new GraphServiceClient(null);
            graphServiceClient.AuthenticationProvider = new DelegateAuthenticationProvider(async (request) => {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
                await Task.FromResult<object>(null);
            });

            return graphServiceClient;
        }
    }
}
