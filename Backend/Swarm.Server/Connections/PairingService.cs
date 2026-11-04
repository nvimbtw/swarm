using System.Security.Cryptography;
using System.Text;

namespace Swarm.Server.Connections;

// Deliberately in-memory until database support lands. Only hashes of bearer secrets are retained.
public sealed class PairingService(TimeProvider clock)
{
    private readonly object _gate = new();
    private readonly Dictionary<string, PendingPairing> _pending = new();
    private readonly Dictionary<string, InstallationIdentity> _credentials = new();

    public PairingChallenge Start(string machineId, string machineName)
    {
        lock (_gate)
        {
            foreach (string id in _pending.Where(x => x.Value.ExpiresAt <= clock.GetUtcNow())
                         .Select(x => x.Key).ToArray())
                _pending.Remove(id);
            if (_pending.Count >= 1000) throw new InvalidOperationException("Pairing capacity reached.");
            string pairingId = Guid.NewGuid().ToString("N");
            string secret = NewSecret();
            var expires = clock.GetUtcNow().AddMinutes(5);
            _pending.Add(pairingId, new(machineId, machineName, Hash(secret), expires));
            return new(pairingId, secret, expires);
        }
    }

    public DiscordPairingCode? Approve(string pairingId, string discordUserId)
    {
        lock (_gate)
        {
            if (!_pending.TryGetValue(pairingId, out var pending) ||
                pending.ExpiresAt <= clock.GetUtcNow() || pending.OwnerId is not null) return null;
            string code = RandomNumberGenerator.GetInt32(100_000_000).ToString("D8");
            pending.OwnerId = discordUserId;
            pending.CodeHash = Hash(code);
            return new(code, pending.MachineName, pending.ExpiresAt);
        }
    }

    public IssuedCredential? Complete(string pairingId, string secret, string code)
    {
        lock (_gate)
        {
            if (!_pending.TryGetValue(pairingId, out var pending) ||
                pending.ExpiresAt <= clock.GetUtcNow() || !Matches(secret, pending.SecretHash)) return null;
            if (pending.OwnerId is null) return null;
            if (!Matches(code, pending.CodeHash!))
            {
                if (++pending.Attempts >= 5) _pending.Remove(pairingId);
                return null;
            }
            // Replace this installation's previous credential when it pairs again.
            foreach (var key in _credentials.Where(x => x.Value.OwnerId == pending.OwnerId &&
                         x.Value.MachineId == pending.MachineId).Select(x => x.Key).ToArray())
                _credentials.Remove(key);
            string token = NewSecret();
            var identity = new InstallationIdentity(pending.OwnerId, pending.MachineId, pending.MachineName);
            _credentials.Add(Hash(token), identity);
            _pending.Remove(pairingId);
            return new(token, identity.OwnerId);
        }
    }

    public InstallationIdentity? Authenticate(string token)
    {
        lock (_gate) return _credentials.GetValueOrDefault(Hash(token));
    }

    private static string NewSecret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static bool Matches(string value, string hash) =>
        CryptographicOperations.FixedTimeEquals(Convert.FromHexString(Hash(value)), Convert.FromHexString(hash));

    private sealed class PendingPairing(string machineId, string machineName, string secretHash, DateTimeOffset expiresAt)
    {
        public string MachineId { get; } = machineId;
        public string MachineName { get; } = machineName;
        public string SecretHash { get; } = secretHash;
        public DateTimeOffset ExpiresAt { get; } = expiresAt;
        public string? OwnerId { get; set; }
        public string? CodeHash { get; set; }
        public int Attempts { get; set; }
    }
}

public sealed record PairingChallenge(string PairingId, string Secret, DateTimeOffset ExpiresAt);
public sealed record DiscordPairingCode(string Code, string MachineName, DateTimeOffset ExpiresAt);
public sealed record IssuedCredential(string Token, string OwnerId);
public sealed record InstallationIdentity(string OwnerId, string MachineId, string MachineName);
