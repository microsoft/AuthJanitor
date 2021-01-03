// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.Threading.Tasks;

namespace AuthJanitor.Agents
{
    public interface IAgentCommunicationProvider
    {
        Task Send(AgentMessageEnvelope envelope);
        Task<AgentMessageEnvelope> TryReceive();
    }
}
