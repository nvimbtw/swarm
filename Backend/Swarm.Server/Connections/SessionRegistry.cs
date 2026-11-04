namespace Swarm.Server.Connections;

public sealed class SessionRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, RegisteredSession> _sessions = new();

    public RegisteredSession Register(Guid connectionId, InstallationIdentity identity, SessionRegistration request)
    {
        lock (_gate)
        {
            if (_sessions.TryGetValue(request.SessionId, out var existing) &&
                (existing.Identity.OwnerId != identity.OwnerId || existing.Identity.MachineId != identity.MachineId))
                throw new InvalidOperationException("Session ID is already owned by another installation.");
            // A reconnect can arrive before the old transport's finally block. Rebind the same
            // installation's ID; old-connection cleanup must never remove the new registration.
            if (!_sessions.ContainsKey(request.SessionId) &&
                _sessions.Values.Count(x => x.Identity.OwnerId == identity.OwnerId &&
                    x.Identity.MachineId == identity.MachineId) >= 100)
                throw new InvalidOperationException("Session limit reached.");
            var session = new RegisteredSession(request.SessionId, connectionId, identity,
                request.Name, request.ProjectId, request.Harness, request.Mode, DateTimeOffset.UtcNow);
            _sessions[request.SessionId] = session;
            return session;
        }
    }

    public bool Remove(Guid connectionId, Guid sessionId)
    {
        lock (_gate)
            return _sessions.TryGetValue(sessionId, out var session) &&
                session.ConnectionId == connectionId && _sessions.Remove(sessionId);
    }

    public void Disconnect(Guid connectionId)
    {
        lock (_gate)
            foreach (var id in _sessions.Where(x => x.Value.ConnectionId == connectionId).Select(x => x.Key).ToArray())
                _sessions.Remove(id);
    }

    public RegisteredSession[] List(string ownerId)
    {
        lock (_gate) return _sessions.Values.Where(x => x.Identity.OwnerId == ownerId).ToArray();
    }
}

public sealed record SessionRegistration(Guid SessionId, string Name, string ProjectId, string Harness, string Mode);
public sealed record RegisteredSession(Guid SessionId, Guid ConnectionId, InstallationIdentity Identity,
    string Name, string ProjectId, string Harness, string Mode, DateTimeOffset ConnectedAt);
