using System.Security.Cryptography;
using System.Text;
using Swarm.Server.Classi;
using Swarm.Server.Db.Repositories;

namespace Swarm.Server.Connections;

public sealed class PairingService(TimeProvider clock, CredentialRepository credentials)
{
    private readonly object _gate = new();
    private readonly Dictionary<string, PendingPairing> _pending = new();

    // Ustvari novo zahtevo za povezovanje.
    public PairingChallenge Start(string machineId, string machineName)
    {
        lock (_gate)
        {
            foreach (string id in _pending.Where(x => x.Value.ExpiresAt <= clock.GetUtcNow())
                         .Select(x => x.Key).ToArray())
                _pending.Remove(id);

            if (_pending.Count >= 1000)
                throw new InvalidOperationException("Pairing capacity reached.");

            string pairingId = Guid.NewGuid().ToString("N");
            string secret = NewSecret();
            var expires = clock.GetUtcNow().AddMinutes(5);

            _pending.Add(pairingId, new PendingPairing(machineId, machineName, Hash(secret), expires));

            return new(pairingId, secret, expires);
        }
    }

    // Odobri povezovanje prek Discorda.
    public DiscordPairingCode? Approve(string pairingId, string discordUserId)
    {
        lock (_gate)
        {
            if (!_pending.TryGetValue(pairingId, out var pending) ||
                pending.ExpiresAt <= clock.GetUtcNow() ||
                pending.OwnerId is not null || pending.Completing)
                return null;

            string code = RandomNumberGenerator.GetInt32(100_000_000).ToString("D8");

            pending.OwnerId = discordUserId;
            pending.CodeHash = Hash(code);

            return new(code, pending.MachineName, pending.ExpiresAt);
        }
    }

    // Zaključi povezovanje in shrani žeton v bazo.
    public async Task<IssuedCredential?> CompleteAsync(string pairingId, string secret, string code,
        CancellationToken ct = default)
    {
        PendingPairing pending;
        string ownerId;

        lock (_gate)
        {
            if (!_pending.TryGetValue(pairingId, out var found) ||
                found.ExpiresAt <= clock.GetUtcNow() ||
                found.OwnerId is null || found.CodeHash is null ||
                found.Completing || !Matches(secret, found.SecretHash))
                return null;

            if (!Matches(code, found.CodeHash))
            {
                if (++found.Attempts >= 5)
                    _pending.Remove(pairingId);

                return null;
            }

            found.Completing = true;
            pending = found;
            ownerId = found.OwnerId;
        }

        string token = NewSecret();

        try
        {
            await credentials.IssueCredentialAsync(
                ownerId, pending.MachineId, pending.MachineName, Hash(token), ct);

            lock (_gate)
                _pending.Remove(pairingId);

            return new(token, ownerId);
        }
        catch
        {
            lock (_gate)
                pending.Completing = false;

            throw;
        }
    }

    // Preveri žeton v bazi.
    public Task<InstallationIdentity?> AuthenticateAsync(string token, CancellationToken ct = default)
    {
        return credentials.AuthenticateAsync(Hash(token), ct);
    }

    private static string NewSecret() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool Matches(string value, string hash) =>
        CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(Hash(value)),
            Convert.FromHexString(hash));
}
