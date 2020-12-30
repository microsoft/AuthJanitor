// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.CryptographicImplementations;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthJanitor.Agents
{
    public class AgentMessageEnvelope
    {
        public DateTimeOffset Created { get; set; }

        public string Originator { get; set; }
        public string Target { get; set; }

        public string MessageType { get; set; }
        public byte[] Message { get; set; } = new byte[0];
        public IAgentMessage MessageObject { get; set; } = null;

        public byte[] Signature { get; set; }


        public static async Task<AgentMessageEnvelope> Create(
            ICryptographicImplementation cryptographicImplementation,
            string originator,
            string target,
            IAgentMessage message)
        {
            var envelope = new AgentMessageEnvelope()
            {
                Created = DateTimeOffset.UtcNow,
                Originator = originator,
                Target = target,
                MessageType = message.GetType().Name,
                Message = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message))
            };

            envelope.Signature = await cryptographicImplementation.Sign(
                await GetMessageEnvelopeHash(cryptographicImplementation, envelope));

            return envelope;
        }

        public async Task<bool> VerifyAndUnpack(
            ICryptographicImplementation cryptographicImplementation)
        {
            MessageObject = null;
            if (!await Verify(cryptographicImplementation))
                return false;
            MessageObject = (IAgentMessage)JsonConvert.DeserializeObject(
                Encoding.UTF8.GetString(Message),
                Type.GetType(MessageType));
            return true;
        }

        public async Task<bool> Verify(
            ICryptographicImplementation cryptographicImplementation) =>
            await cryptographicImplementation.Verify(
                await GetMessageEnvelopeHash(cryptographicImplementation, this),
                this.Signature);

        private static Task<byte[]> GetMessageEnvelopeHash(
            ICryptographicImplementation cryptographicImplementation,
            AgentMessageEnvelope envelope) =>
            cryptographicImplementation.Hash(
                new byte[][] {
                    BitConverter.GetBytes(envelope.Created.Ticks),
                    Encoding.UTF8.GetBytes(envelope.Originator),
                    Encoding.UTF8.GetBytes(envelope.Target),
                    Encoding.UTF8.GetBytes(envelope.MessageType),
                    envelope.Message
                }.SelectMany(a => a).ToArray());
    }
}
