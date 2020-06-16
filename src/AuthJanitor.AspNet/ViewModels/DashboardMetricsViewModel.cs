// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.Collections.Generic;

namespace AuthJanitor.UI.Shared.ViewModels
{
    public class DashboardMetricsViewModel : IAuthJanitorViewModel
    {
        public string SignedInName { get; set; } = "John Doe";
        public string SignedInEmail { get; set; } = "john.doe@contoso.com";
        public string SignedInRoles { get; set; } = "NoRole";

        public int TotalSecrets { get; set; }
        public int TotalResources { get; set; }
        public int TotalExpiringSoon { get; set; }
        public int TotalExpired { get; set; }
        public int TasksInError { get; set; }

        public int TotalPendingApproval { get; set; }
        public int PercentExpired { get; set; }

        public int Risk0 { get; set; }
        public int Risk35 { get; set; }
        public int Risk60 { get; set; }
        public int Risk85 { get; set; }
        public int RiskOver85 { get; set; }

        public IEnumerable<ManagedSecretViewModel> ExpiringSoon { get; set; } = new List<ManagedSecretViewModel>();
    }
}
