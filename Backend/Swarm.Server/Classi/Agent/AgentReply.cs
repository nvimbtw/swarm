namespace Swarm.Server.Classi;

public sealed record AgentReply(
    Guid RequestId,
    string Status,
    string? Error = null
);