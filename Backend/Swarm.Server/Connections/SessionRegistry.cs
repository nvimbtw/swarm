using Swarm.Server.Classi;
using Swarm.Server.Db.Repositories;

namespace Swarm.Server.Connections;

public sealed class SessionRegistry(SessionRepository data)
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, RegisteredSession> _sessions = new();

    public async Task<RegisteredSession> RegisterAsync(Guid connectionId, InstallationIdentity identity,
        SessionRegistration request, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (_sessions.TryGetValue(request.SessionId, out var existing) &&
                (existing.Identity.OwnerId != identity.OwnerId || existing.Identity.MachineId != identity.MachineId))
                throw new InvalidOperationException("Session ID is already owned by another installation.");
            if (!_sessions.ContainsKey(request.SessionId) && _sessions.Values.Count(x =>
                x.Identity.OwnerId == identity.OwnerId && x.Identity.MachineId == identity.MachineId) >= 100)
                throw new InvalidOperationException("Session limit reached.");
        }

        await data.RegisterSessionAsync(identity, request, ct);
        var session = new RegisteredSession(request.SessionId, connectionId, identity, request.Name,
            request.ProjectId, request.Harness, request.Mode, DateTimeOffset.UtcNow, request.Control);
        lock (_gate) _sessions[request.SessionId] = session;
        return session;
    }

    public async Task<bool> RemoveAsync(Guid connectionId, Guid sessionId, CancellationToken ct = default)
    {
        bool removed;
        lock (_gate)
            removed = _sessions.TryGetValue(sessionId, out var session) &&
                session.ConnectionId == connectionId && _sessions.Remove(sessionId);
        if (removed) await data.SetSessionStateAsync(sessionId, "disconnected", ct);
        return removed;
    }

    public async Task DisconnectAsync(Guid connectionId, CancellationToken ct = default)
    {
        Guid[] ids;
        lock (_gate)
        {
            ids = _sessions.Where(x => x.Value.ConnectionId == connectionId).Select(x => x.Key).ToArray();
            foreach (var id in ids) _sessions.Remove(id);
        }
        foreach (var id in ids) await data.SetSessionStateAsync(id, "disconnected", ct);
    }

    public static bool CanUse(RegisteredSession session, string userId) =>
        session.Control && session.Harness == "codex" &&
        (session.Identity.OwnerId == userId || session.Mode == "swarm");

    public RegisteredSession? FindAvailable(string userId, Guid sessionId)
    {
        lock (_gate) return _sessions.TryGetValue(sessionId, out var session) && CanUse(session, userId) ? session : null;
    }

    public RegisteredSession[] Available(string userId)
    {
        lock (_gate) return _sessions.Values.Where(x => CanUse(x, userId)).ToArray();
    }

    public RegisteredSession[] List(string ownerId)
    {
        lock (_gate) return _sessions.Values.Where(x => x.Identity.OwnerId == ownerId).ToArray();
    }
}
