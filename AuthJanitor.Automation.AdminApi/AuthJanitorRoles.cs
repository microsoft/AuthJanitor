// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;

namespace AuthJanitor.Automation.AdminApi
{
    public sealed class AuthJanitorRoles
    {
        public static readonly string GlobalAdmin = "globalAdmin";
        public static readonly string ResourceAdmin = "resourceAdmin";
        public static readonly string SecretAdmin = "secretAdmin";
        public static readonly string ServiceOperator = "serviceOperator";
        public static readonly string Auditor = "auditor";
    }
    public static class AuthJanitorRoleExtensions
    {
#if DEBUG
        private static bool IsRunningLocally => string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));
#endif

        public static string GetUserRole(HttpRequest req)
        {
#if DEBUG
            if (IsRunningLocally) return AuthJanitorRoles.GlobalAdmin;
#endif
            if (req.HttpContext.User == null ||
                req.HttpContext.User.Claims == null ||
                !req.HttpContext.User.Claims.Any()) return string.Empty;
            var roles = req.HttpContext.User.Claims.FirstOrDefault(c => c.Type == "roles");
            if (roles == null || roles.Value == null || !roles.Value.Any()) return string.Empty;
            return roles.Value;
        }

        public static bool IsValidUser(this HttpRequest req)
        {
#if DEBUG
            if (IsRunningLocally) return true;
#endif
            var role = GetUserRole(req);
            if (role == AuthJanitorRoles.GlobalAdmin ||
                role == AuthJanitorRoles.ResourceAdmin ||
                role == AuthJanitorRoles.SecretAdmin ||
                role == AuthJanitorRoles.ServiceOperator ||
                role == AuthJanitorRoles.Auditor)
                return true;
            return false;
        }
        public static bool IsValidUser(this HttpRequest req, params string[] validRoles)
        {
#if DEBUG
            if (IsRunningLocally) return true;
#endif
            var role = GetUserRole(req);
            if (string.IsNullOrEmpty(role)) return false;
            if (validRoles.Any(validRole => role == validRole))
                return true;
            return false;
        }
    }
}
