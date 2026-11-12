namespace Swarm.Server.Classi;

public sealed record DiscordPairingCode(
    string Code,
    string MachineName,
    DateTimeOffset ExpiresAt
);