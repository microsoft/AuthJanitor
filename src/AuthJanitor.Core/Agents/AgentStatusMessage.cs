// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;

namespace AuthJanitor.Agents
{
    public class AgentStatusMessage : IAgentMessage
    {
        public DateTimeOffset CurrentTime { get; set; }
    }
}
