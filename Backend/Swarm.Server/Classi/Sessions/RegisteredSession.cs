namespace Swarm.Server.Classi;

public sealed record RegisteredSession(
    Guid SessionId,
    Guid ConnectionId,
    InstallationIdentity Identity,
    string Name,
    string ProjectId,
    string Harness,
    string Mode,
    DateTimeOffset ConnectedAt,
    bool Control = false
);