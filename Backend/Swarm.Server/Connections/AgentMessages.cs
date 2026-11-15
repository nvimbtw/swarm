
using Swarm.Server.Classi;

namespace Swarm.Server.Connections;

public sealed record AgentAttribution(Guid SessionId, string AgentName, string OwnerId,
    string Harness, string ProjectId, string MachineName, string MachineId)
{
    public static AgentAttribution FromSession(RegisteredSession session) => new(session.SessionId,
        session.Name, session.Identity.OwnerId, session.Harness, session.ProjectId,
        session.Identity.MachineName, session.Identity.MachineId);
}
public interface IAgentEventSink
{
    ValueTask PublishAsync(
        AgentDestination destination,
        AgentEvent update
    );
}
