using System;
using System.Linq;
using System.Threading.Tasks;
using AuthJanitor.AgentBridges;
using AuthJanitor.IdentityServices;
using AuthJanitor.Integrations.CryptographicImplementations;
using AuthJanitor.Providers;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AuthJanitor
{
    public class PerformProviderWorkflow
    {
        private ICryptographicImplementation _cryptographicImplementation;
        private IIdentityService _identityService;
        private ProviderManagerService _providerManagerService;

        public PerformProviderWorkflow(
            ICryptographicImplementation cryptographicImplementation,
            IIdentityService identityService,
            ProviderManagerService providerManagerService)
        {
            _cryptographicImplementation = cryptographicImplementation;
            _identityService = identityService;
            _providerManagerService = providerManagerService;
        }

        [FunctionName("PerformProviderWorkflow")]
        public async Task Run(
            [QueueTrigger("%agent-receive-queue%", Connection = "AuthJanitorAgentBridge")] string agentQueueItem,
            ILogger log)
        {
            // Set "agent-receive-queue" in settings
            var message = await GetAgentActionMessage(agentQueueItem);

            var logger = new RekeyingAttemptLogger(log);
            var providers = message.Providers.Select(p => _providerManagerService.GetProviderInstance(p.ProviderType, p.ProviderConfiguration)).ToList();

            var agentCredential = await _identityService.GetAccessTokenForApplicationAsync();
            if (agentCredential == null || string.IsNullOrEmpty(agentCredential.AccessToken))
                throw new Exception("Could not acquire access token for Agent application");
            providers.ForEach(p => p.Credential = agentCredential);

            // todo: relay log output messages to send queue
            await _providerManagerService.ExecuteRekeyingWorkflow(logger, message.ValidPeriod, providers);
        }

        private async Task<AgentActionMessage> GetAgentActionMessage(string queueItemContent)
        {
            var envelope = JsonConvert.DeserializeObject<MessageEnvelope>(queueItemContent);
            byte[] ajServicePublicKey = new byte[0]; // get from settings
            byte[] agentPrivateKey = new byte[0]; // get from settings

            if (await _cryptographicImplementation.Verify(ajServicePublicKey, envelope.Message, envelope.Signature))
            {
                var decryptedMessage = await _cryptographicImplementation.Decrypt(agentPrivateKey, envelope.Message);
                return JsonConvert.DeserializeObject<AgentActionMessage>(decryptedMessage);
            }
            throw new Exception("Signature verification failed!");
        }
    }
}
