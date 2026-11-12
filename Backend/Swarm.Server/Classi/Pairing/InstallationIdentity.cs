namespace Swarm.Server.Classi;

public sealed record InstallationIdentity(
    string OwnerId,
    string MachineId,
    string MachineName
);