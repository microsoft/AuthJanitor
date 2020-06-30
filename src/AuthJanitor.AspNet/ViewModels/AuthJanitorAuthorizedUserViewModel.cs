// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
namespace AuthJanitor.UI.Shared.ViewModels
{
    public class AuthJanitorAuthorizedUserViewModel : IAuthJanitorViewModel
    {
        public string DisplayName { get; set; }
        public string UPN { get; set; }
        public string RoleValue { get; set; }
    }
}
