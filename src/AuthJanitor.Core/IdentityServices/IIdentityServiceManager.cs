// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthJanitor.IdentityServices
{
    public interface IIdentityServiceManager
    {
        Task AddAuthorizedUser(string userPrincipalName, string role);
        Task<IList<AuthJanitorAuthorizedUser>> GetAuthorizedUsers();
        Task RemoveAuthorizedUser(string userPrincipalName);
        Task RemoveAuthorizedUser(string userPrincipalName, string role);
    }
}
