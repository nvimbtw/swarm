
using Swarm.Server.Classi;

namespace Swarm.Server.Db.Repositories;

public sealed class WorkRequestRepository(DatabaseService database)
{
    public async Task CreateWorkRequestAsync(Guid requestId, string requesterId,
        AgentRequest request, CancellationToken ct = default)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync(ct);
        using var transaction = connection.BeginTransaction();

        long userId = await DatabaseQueries.EnsureUserAsync(connection, transaction, requesterId, ct);

        await DatabaseQueries.ExecuteAsync(connection, transaction, @"
            INSERT INTO WorkRequest (ID, TargetSessionID, RequesterUserID, Kind, DeliveryMode, Prompt,
                Status, Model, Reasoning)
            VALUES ($id, $session, $user, 'agent.submit', $delivery, $prompt, 'sending', $model, $reasoning);",
            ct, ("$id", requestId.ToString()), ("$session", request.SessionId.ToString()),
            ("$user", userId), ("$delivery", request.Delivery), ("$prompt", request.Prompt),
            ("$model", (object?)request.Model ?? DBNull.Value),
            ("$reasoning", (object?)request.Reasoning ?? DBNull.Value));

        transaction.Commit();
    }

    public async Task RecordRequestUpdateAsync(Guid requestId, string status, string? output = null,
        string? error = null, CancellationToken ct = default)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync(ct);
        using var transaction = connection.BeginTransaction();

        string now = DateTimeOffset.UtcNow.ToString("O");

        await DatabaseQueries.ExecuteAsync(connection, transaction, @"
            UPDATE WorkRequest SET Status=$status, Error=$error,
                DeliveredAt=CASE WHEN $status IN ('queued','injected') THEN COALESCE(DeliveredAt,$now) ELSE DeliveredAt END,
                StartedAt=CASE WHEN $status='started' THEN COALESCE(StartedAt,$now) ELSE StartedAt END,
                FinishedAt=CASE WHEN $status IN ('completed','failed','interrupted','unknown') THEN $now ELSE FinishedAt END
            WHERE ID=$id;", ct, ("$status", status), ("$error", (object?)error ?? DBNull.Value),
            ("$now", now), ("$id", requestId.ToString()));

        await DatabaseQueries.ExecuteAsync(connection, transaction, @"
            INSERT INTO RequestEvent (RequestID, Kind, SummaryMarkdown, Output, Error, CreatedAt)
            VALUES ($id, $status, $summary, $output, $error, $now);", ct,
            ("$id", requestId.ToString()), ("$status", status), ("$summary", status),
            ("$output", (object?)output ?? DBNull.Value), ("$error", (object?)error ?? DBNull.Value),
            ("$now", now));

        transaction.Commit();
    }
}
