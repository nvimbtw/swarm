namespace Swarm.Server.Classi;

public sealed record AgentEvent(
    Guid RequestId,
    Guid SessionId,
    string Status,
    string? Output = null,
    string? Error = null,
    string? TurnId = null
);