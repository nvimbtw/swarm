namespace Swarm.Server.Classi;

public sealed record PairingChallenge(
    string PairingId,
    string Secret,
    DateTimeOffset ExpiresAt
);