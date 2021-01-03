// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Agents;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace AuthJanitor.IdentityServices
{
    public static class DummyAgentCommunicationProviderExtensions
    {
        public static void AddDummyAgentCommunicationProvider(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IAgentCommunicationProvider, DummyAgentCommunicationProvider>();
        }
    }

    public class DummyAgentCommunicationProvider : IAgentCommunicationProvider
    {
        public Task Send(AgentMessageEnvelope envelope) => Task.FromResult(false);

        public Task<AgentMessageEnvelope> TryReceive() => Task.FromResult(new AgentMessageEnvelope());
    }
}
