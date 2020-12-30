// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers;
using System;
using System.Collections.Generic;

namespace AuthJanitor.Agents
{
    public class AgentProviderCommandMessage : IAgentMessage
    {
        public List<ProviderExecutionParameters> Providers { get; set; } =
            new List<ProviderExecutionParameters>();

        public TimeSpan ValidPeriod { get; set; }
        public string State { get; set; }
    }
}
