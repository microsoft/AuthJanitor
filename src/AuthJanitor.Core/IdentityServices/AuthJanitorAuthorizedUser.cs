// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
namespace AuthJanitor.IdentityServices
{
    public class AuthJanitorAuthorizedUser
    {
        public string DisplayName { get; set; }
        public string UPN { get; set; }
        public string Role { get; set; }
        public string RoleValue { get; set; }
    }
}
