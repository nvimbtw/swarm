namespace Swarm.Server.Classi;

public sealed record AgentDestination(
    string SenderId,
    ulong ChannelId
);