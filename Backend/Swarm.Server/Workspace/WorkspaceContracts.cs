using Swarm.Server.Connections;
using Swarm.Server.Classi;

namespace Swarm.Server.Workspace;

public sealed class WorkspaceException(string code) : Exception(code);

public sealed record WorkspaceRequest(Guid SessionId, long? Id = null, string? Title = null,
    string? BriefMarkdown = null, string? Markdown = null, string? Type = null, string? Status = null,
    int? Priority = null, long? InitiativeId = null, bool ClearInitiative = false,
    int? ExpectedVersion = null, string? Query = null, int Limit = 20, long AfterId = 0);

public sealed record WorkspaceItem(long Id, long ProjectId, string Title, int Version,
    string CreatedAt, string UpdatedAt, string? BriefMarkdown = null, string? Markdown = null,
    string? Type = null, string? Status = null, int? Priority = null, long? InitiativeId = null,
    string? InitiativeTitle = null);
public sealed record WorkspacePage(IReadOnlyList<WorkspaceItem> Items, long Total, long? NextAfterId);

public interface IWorkspaceRepository
{
    Task<object> ExecuteAsync(RegisteredSession session, string kind, WorkspaceRequest request, CancellationToken ct);
}

public sealed class WorkspaceService(SessionRegistry sessions, IWorkspaceRepository repository)
{
    public Task<object> ExecuteAsync(Guid connectionId, string kind, WorkspaceRequest request, CancellationToken ct)
    {
        var session = sessions.FindConnected(connectionId, request.SessionId)
            ?? throw new WorkspaceException("session_unavailable_or_not_allowed");
        return repository.ExecuteAsync(session, kind, request, ct);
    }
}
