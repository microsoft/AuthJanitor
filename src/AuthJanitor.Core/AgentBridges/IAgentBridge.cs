// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Integrations.CryptographicImplementations;
using AuthJanitor.SecureStorage;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace AuthJanitor.AgentBridges
{
    public interface IAgentBridge
    {
        Task<MessageEnvelope> CreateAgentMessage(string agentId, TimeSpan secretValidPeriod, params AgentActionMessageProvider[] providers);
        Task<AgentActionMessage> DecryptAgentMessage(MessageEnvelope envelope);
    }

    public class AzureQueuesAgentBridge : IAgentBridge
    {
        private ICryptographicImplementation _cryptographicImplementation;
        private ISecureStorage _secureStorage;

        public AzureQueuesAgentBridge(
            ICryptographicImplementation cryptographicImplementation,
            ISecureStorage secureStorage)
        {
            _cryptographicImplementation = cryptographicImplementation;
            _secureStorage = secureStorage;
        }

        public async Task<AgentActionMessage> DecryptAgentMessage(MessageEnvelope envelope)
        {
            var serverPublicKey = new byte[0];
            var agentPrivateKey = new byte[0];
            if (!await _cryptographicImplementation.Verify(serverPublicKey, envelope.Message, envelope.Signature))
            {
                throw new Exception("Signature invalid!");
            }
            var decrypted = await _cryptographicImplementation.Decrypt(agentPrivateKey, envelope.Message);

            return JsonSerializer.Deserialize<AgentActionMessage>(decrypted);
        }

        public async Task<MessageEnvelope> CreateAgentMessage(string agentId, TimeSpan secretValidPeriod, params AgentActionMessageProvider[] providers)
        {
            var serverPrivateKey = new byte[0];
            var agentPublicKey = new byte[0];

            var agentActionMessage = new AgentActionMessage(agentId, secretValidPeriod, providers);
            var encrypted = await _cryptographicImplementation.Encrypt(agentPublicKey, JsonSerializer.Serialize(agentActionMessage));
            var signature = await _cryptographicImplementation.Sign(serverPrivateKey, encrypted);

            return new MessageEnvelope(encrypted, signature);
        }
    }
}
