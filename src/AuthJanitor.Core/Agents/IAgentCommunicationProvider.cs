using System.Threading.Tasks;

namespace AuthJanitor.Agents
{
    public interface IAgentCommunicationProvider
    {
        Task Send(AgentMessageEnvelope envelop);
        Task<AgentMessageEnvelope> TryReceive();
    }
}
