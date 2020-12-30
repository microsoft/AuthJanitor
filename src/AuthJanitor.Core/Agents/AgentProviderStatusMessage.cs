// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using AuthJanitor.Providers;

namespace AuthJanitor.Agents
{
    public class AgentProviderStatusMessage : IAgentMessage
    {
        public string State { get; set; }

        public ProviderWorkflowActionCollection WorkflowActionCollection { get; set; } =
            new ProviderWorkflowActionCollection();
    }
}
