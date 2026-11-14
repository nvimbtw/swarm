namespace Swarm.Server.Classi;

public sealed record SessionRegistration(
    Guid SessionId,
    string Name,
    string ProjectId,
    string Harness,
    string Mode,
    bool Control = false
);