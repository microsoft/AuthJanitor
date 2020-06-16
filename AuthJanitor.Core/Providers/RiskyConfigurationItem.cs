// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
namespace AuthJanitor.Providers
{
    public class RiskyConfigurationItem
    {
        /// <summary>
        /// Risk score
        /// </summary>
        public int Score { get; set; }

        /// <summary>
        /// Description of risk
        /// </summary>
        public string Risk { get; set; }

        /// <summary>
        /// Recommendation to remediate risk
        /// </summary>
        public string Recommendation { get; set; }
    }
}
