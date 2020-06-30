// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.IdentityServices;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace AuthJanitor.Services
{
    public class IdentityManagementService
    {
        private readonly IIdentityService _identityService;
        private readonly IIdentityServiceManager _identityServiceManager;

        public IdentityManagementService(
            IIdentityService identityService,
            IIdentityServiceManager identityServiceManager)
        {
            _identityService = identityService;
            _identityServiceManager = identityServiceManager;
        }

        public async Task<IActionResult> GetAuthorizedUsers()
        {
            try
            {
                if (!_identityService.CurrentUserHasRole(AuthJanitorRoles.GlobalAdmin)) return new UnauthorizedResult();

                return new OkObjectResult(await _identityServiceManager.GetAuthorizedUsers());
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(ex);
            }
        }

        public async Task<IActionResult> AddAuthorizedUser(string userPrincipalName, string role)
        {
            try
            {
                if (!_identityService.CurrentUserHasRole(AuthJanitorRoles.GlobalAdmin)) return new UnauthorizedResult();

                await _identityServiceManager.AddAuthorizedUser(
                    System.Net.WebUtility.UrlEncode(userPrincipalName), 
                    role);
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(ex);
            }
            return new OkResult();
        }

        public async Task<IActionResult> RemoveAuthorizedUser(string userPrincipalName)
        {
            try
            {
                if (!_identityService.CurrentUserHasRole(AuthJanitorRoles.GlobalAdmin)) return new UnauthorizedResult();

                await _identityServiceManager.RemoveAuthorizedUser(System.Net.WebUtility.UrlEncode(userPrincipalName));
            }
            catch (Exception ex) 
            {
                return new BadRequestObjectResult(ex);
            }
            return new OkResult();
        }

        public async Task<IActionResult> RemoveAuthorizedUser(string userPrincipalName, string role)
        {
            try
            {
                if (!_identityService.CurrentUserHasRole(AuthJanitorRoles.GlobalAdmin)) return new UnauthorizedResult();

                await _identityServiceManager.RemoveAuthorizedUser(
                    System.Net.WebUtility.UrlEncode(userPrincipalName),
                    role);
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(ex);
            }
            return new OkResult();
        }
    }
}
