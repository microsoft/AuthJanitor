// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace AuthJanitor.IdentityServices
{
    public sealed class AuthJanitorRoles
    {
        public static readonly string GlobalAdmin = "globalAdmin";
        public static readonly string ResourceAdmin = "resourceAdmin";
        public static readonly string SecretAdmin = "secretAdmin";
        public static readonly string ServiceOperator = "serviceOperator";
        public static readonly string Auditor = "auditor";

        public static readonly string[] ALL_ROLES = new string[]
        {
            GlobalAdmin,
            ResourceAdmin,
            SecretAdmin,
            ServiceOperator,
            Auditor
        };
    }
}
