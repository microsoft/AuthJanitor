// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
using System.Collections.Generic;

namespace AuthJanitor.AgentBridges
{
    public class AgentActionMessage
    {
        public string AgentId { get; set; }
        public TimeSpan ValidPeriod { get; set; }
        public IEnumerable<AgentActionMessageProvider> Providers { get; set; } = new AgentActionMessageProvider[0];

        public AgentActionMessage() { }
        public AgentActionMessage(string agentId, TimeSpan secretValidPeriod, params AgentActionMessageProvider[] providers)
        {
            AgentId = agentId;
            ValidPeriod = secretValidPeriod;
            Providers = providers;
        }
    }
    public class AgentActionMessageProvider
    {
        public string ProviderType { get; set; }
        public string ProviderConfiguration { get; set; }

        public AgentActionMessageProvider() { }
        public AgentActionMessageProvider(string providerType, string providerConfiguration)
        {
            ProviderType = providerType;
            ProviderConfiguration = providerConfiguration;
        }
    }

    public class MessageEnvelope
    {
        public string Signature { get; set; }
        public string Message { get; set; }

        public MessageEnvelope() { }
        public MessageEnvelope(string signature, string message)
        {
            Signature = signature;
            Message = message;
        }
    }
}
