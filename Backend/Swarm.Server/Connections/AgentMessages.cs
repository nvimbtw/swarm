
using Swarm.Server.Classi;

namespace Swarm.Server.Connections;

// Vmesnik za pošiljanje posodobitev o zahtevah.
public interface IAgentEventSink
{
    ValueTask PublishAsync(
        AgentDestination destination,
        AgentEvent update
    );
}
