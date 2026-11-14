namespace Swarm.Server.Classi;

public sealed record AgentRequest(
    Guid SessionId,
    string Prompt,
    string Delivery = "queue",
    string? Model = null,
    string? Reasoning = null
);