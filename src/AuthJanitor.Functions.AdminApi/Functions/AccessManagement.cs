// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.IdentityServices;
using AuthJanitor.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System.Threading.Tasks;

namespace AuthJanitor.Functions
{
    public class AccessManagement
    {
        private readonly IIdentityService _identityService;
        private readonly IdentityManagementService _managementService;

        public AccessManagement(
            IIdentityService identityService,
            IdentityManagementService managementService)
        {
            _identityService = identityService;
            _managementService = managementService;
        }

        [FunctionName("AccessManagement-Add")]
        public async Task<IActionResult> Add([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "access")] AuthJanitorAuthorizedUser newAuthorizedUserRole)
        {
            if (!_identityService.CurrentUserHasRole(AuthJanitorRoles.GlobalAdmin)) return new UnauthorizedResult();

            return await _managementService.AddAuthorizedUser(newAuthorizedUserRole.UPN, newAuthorizedUserRole.RoleValue);
        }

        [FunctionName("AccessManagement-List")]
        public async Task<IActionResult> List([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "access")] HttpRequest req)
        {
            _ = req;

            if (!_identityService.CurrentUserHasRole(AuthJanitorRoles.GlobalAdmin)) return new UnauthorizedResult();

            return await _managementService.GetAuthorizedUsers();
        }

        [FunctionName("AccessManagement-RemoveRole")]
        public async Task<IActionResult> RemoveRole([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "access/removeRole")] AuthJanitorAuthorizedUser userToBeRemoved)
        {
            if (!_identityService.CurrentUserHasRole(AuthJanitorRoles.GlobalAdmin)) return new UnauthorizedResult();

            return await _managementService.RemoveAuthorizedUser(userToBeRemoved.UPN, userToBeRemoved.RoleValue);
        }

        [FunctionName("AccessManagement-RemoveUser")]
        public async Task<IActionResult> RemoveUser([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "access/removeUser")] AuthJanitorAuthorizedUser userToBeRemoved)
        {
            if (!_identityService.CurrentUserHasRole(AuthJanitorRoles.GlobalAdmin)) return new UnauthorizedResult();

            return await _managementService.RemoveAuthorizedUser(userToBeRemoved.UPN);
        }
    }
}
