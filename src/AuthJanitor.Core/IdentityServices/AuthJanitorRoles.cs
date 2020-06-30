// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.Collections.Generic;
using System.Linq;

namespace AuthJanitor.IdentityServices
{
    public sealed class AuthJanitorRoles
    {
        public static readonly string GlobalAdmin = "globalAdmin";
        public static readonly string ResourceAdmin = "resourceAdmin";
        public static readonly string SecretAdmin = "secretAdmin";
        public static readonly string ServiceOperator = "serviceOperator";
        public static readonly string Auditor = "auditor";

        public static readonly Dictionary<string, string> ROLE_NICENAMES = new Dictionary<string, string>()
        {
            { GlobalAdmin, "Global Administrator" },
            { ResourceAdmin, "Resource Administrator" },
            { SecretAdmin, "Secret Administrator" },
            { ServiceOperator, "Service Operator" },
            { Auditor, "Auditor" },
        };

        public static readonly string[] ALL_ROLES = ROLE_NICENAMES.Keys.ToArray();
    }
}
