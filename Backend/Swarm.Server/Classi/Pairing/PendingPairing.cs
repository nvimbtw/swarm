namespace Swarm.Server.Classi;

public sealed class PendingPairing
{
    public string MachineId { get; set; }
    public string MachineName { get; set; }
    public string SecretHash { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    public string? OwnerId { get; set; }
    public string? CodeHash { get; set; }
    public int Attempts { get; set; }
    public bool Completing { get; set; }

    public PendingPairing(string machineId, string machineName, string secretHash, DateTimeOffset expiresAt)
    {
        MachineId = machineId;
        MachineName = machineName;
        SecretHash = secretHash;
        ExpiresAt = expiresAt;
    }
}
