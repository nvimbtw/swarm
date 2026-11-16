using Swarm.Server.Workspace;

namespace Swarm.Server.Db.Repositories;

public sealed partial class WorkspaceRepository
{
    private static void Validate(string entity, string action, WorkspaceRequest request)
    {
        bool needsId = action is "get" or "update" or "delete";
        bool needsVersion = action is "update" or "delete";
        if ((needsId && request.Id is not > 0) || (needsVersion && request.ExpectedVersion is not > 0))
            throw new WorkspaceException("invalid_workspace_request");

        if (request.Limit is < 1 or > 20 || request.AfterId < 0 || request.Query?.Length > 200)
            throw new WorkspaceException("invalid_workspace_request");

        if (action == "create" && string.IsNullOrWhiteSpace(request.Title))
            throw new WorkspaceException("invalid_workspace_request");
        if (request.Title is not null && (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200))
            throw new WorkspaceException("invalid_workspace_request");
        if (request.BriefMarkdown?.Length > 4000 || request.Markdown?.Length > 8000)
            throw new WorkspaceException("invalid_workspace_request");

        if (entity != "work_item") return;
        if (request.Priority is < 0 or > 5 || request.InitiativeId is <= 0 ||
            (request.ClearInitiative && request.InitiativeId is not null))
            throw new WorkspaceException("invalid_workspace_request");
        if ((action == "create" && request.Type is null) ||
            (request.Type is not null && request.Type is not ("issue" or "feature")))
            throw new WorkspaceException("invalid_workspace_request");
        if (request.Status is not null && request.Status is not ("open" or "in-progress" or "verification" or "closed"))
            throw new WorkspaceException("invalid_workspace_request");
    }
}
