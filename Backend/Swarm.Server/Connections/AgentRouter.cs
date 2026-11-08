using System.Collections.Concurrent;
using System.Text.Json;

namespace Swarm.Server.Connections;

/// <summary>Multiplexes correlated requests/events over the registered installation's socket.</summary>
public sealed class AgentRouter(SessionRegistry registry, IAgentEventSink sink)
{
    private sealed record Transport(Func<object, CancellationToken, Task> Send, Func<bool> Authenticated);
    private sealed record PendingRequest(Guid ConnectionId, Guid SessionId, AgentDestination Destination,
        TaskCompletionSource<JsonElement> Acknowledgment, DateTimeOffset CreatedAt);
    private readonly ConcurrentDictionary<Guid, Transport> _connections = new();
    private readonly ConcurrentDictionary<Guid, PendingRequest> _pendingRequests = new();
    private readonly SemaphoreSlim _requestAdmission = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan RequestLifetime = TimeSpan.FromHours(24);
    private const int MaxOutstandingRequests = 1000;
    private const int MaxRequestsPerConnection = 100;

    public void Connect(Guid connectionId, Func<object, CancellationToken, Task> send, Func<bool> authenticated) =>
        _connections[connectionId] = new(send, authenticated);

    public async Task DisconnectAsync(Guid connectionId)
    {
        _connections.TryRemove(connectionId, out _);
        foreach (var (requestId, request) in _pendingRequests.Where(x => x.Value.ConnectionId == connectionId).ToArray())
        {
            if (!_pendingRequests.TryRemove(requestId, out _)) continue;
            request.Acknowledgment.TrySetException(new AgentRequestException("connection_lost_delivery_unknown"));
            await sink.PublishAsync(request.Destination, new(requestId, request.SessionId, "unknown",
                Error: "Connection lost. Check local Codex before resending; accepted work is not replayed."));
        }
    }

    public Task<JsonElement> DescribeAsync(string senderId, Guid sessionId, CancellationToken ct = default) =>
        ExchangeAsync(senderId, sessionId, "agent.describe", null, new(senderId, 0), ct);

    public async Task<AgentReply> SubmitAsync(AgentDestination destination, AgentRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt) || request.Prompt.Length > 4000 ||
            request.Delivery is not ("queue" or "inject") || !ValidOptionalSetting(request.Model, 100) ||
            !ValidOptionalSetting(request.Reasoning, 40)) throw new AgentRequestException("invalid_request");
        var result = await ExchangeAsync(destination.SenderId, request.SessionId, "agent.submit", request, destination, ct);
        return new(result.GetProperty("requestId").GetGuid(), result.GetProperty("status").GetString()!,
            result.TryGetProperty("error", out var error) ? error.GetString() : null);
    }

    private static bool ValidOptionalSetting(string? value, int max) => value is null ||
        (!string.IsNullOrWhiteSpace(value) && value.Length <= max && !value.Any(char.IsControl));

    private async Task<JsonElement> ExchangeAsync(string senderId, Guid sessionId, string type,
        AgentRequest? request, AgentDestination destination, CancellationToken ct)
    {
        var session = registry.FindAvailable(senderId, sessionId)
            ?? throw new AgentRequestException("session_unavailable_or_not_allowed");
        if (!_connections.TryGetValue(session.ConnectionId, out var transport) || !transport.Authenticated())
            throw new AgentRequestException("session_offline");
        var requestId = Guid.NewGuid();
        var pending = new PendingRequest(session.ConnectionId, sessionId, destination,
            new(TaskCreationOptions.RunContinuationsAsynchronously), DateTimeOffset.UtcNow);
        await TrackRequestAsync(requestId, pending, ct);
        bool awaitingCompletion = false;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(ReplyTimeout);
            await transport.Send(new
            {
                version = 1,
                id = requestId,
                type,
                payload = new
                {
                    requestId,
                    sessionId,
                    senderId,
                    channelId = destination.ChannelId.ToString(),
                    prompt = request?.Prompt,
                    delivery = request?.Delivery,
                    model = request?.Model,
                    reasoning = request?.Reasoning
                }
            }, timeout.Token);
            var response = await pending.Acknowledgment.Task.WaitAsync(timeout.Token);
            var status = response.GetProperty("status").GetString();
            awaitingCompletion = type == "agent.submit" && (status is "queued" or "injected");
            return response;
        }
        catch (OperationCanceledException)
        {
            throw new AgentRequestException("delivery_unknown_check_local_codex");
        }
        catch (System.Net.WebSockets.WebSocketException)
        {
            throw new AgentRequestException("connection_lost_delivery_unknown");
        }
        finally
        {
            if (!awaitingCompletion) _pendingRequests.TryRemove(requestId, out _);
        }
    }

    private async Task TrackRequestAsync(Guid requestId, PendingRequest request, CancellationToken ct)
    {
        await _requestAdmission.WaitAsync(ct);
        try
        {
            var expiredBefore = DateTimeOffset.UtcNow - RequestLifetime;
            foreach (var (expiredId, pending) in _pendingRequests.Where(x => x.Value.CreatedAt < expiredBefore).ToArray())
            {
                if (_pendingRequests.TryRemove(expiredId, out _))
                    pending.Acknowledgment.TrySetException(new AgentRequestException("request_expired"));
            }
            if (_pendingRequests.Count >= MaxOutstandingRequests ||
                _pendingRequests.Values.Count(x => x.ConnectionId == request.ConnectionId) >= MaxRequestsPerConnection)
                throw new AgentRequestException("installation_busy");
            _pendingRequests[requestId] = request;
        }
        finally { _requestAdmission.Release(); }
    }

    public async Task AcceptAsync(Guid connectionId, string type, JsonElement payload)
    {
        var id = payload.GetProperty("requestId").GetGuid();
        var sessionId = payload.GetProperty("sessionId").GetGuid();
        // Destinations belong to the server-held request, never to client event fields.
        if (!_pendingRequests.TryGetValue(id, out var pending) || pending.ConnectionId != connectionId ||
            pending.SessionId != sessionId) return;
        if (type == "agent.reply")
        {
            pending.Acknowledgment.TrySetResult(payload.Clone());
            return;
        }
        var update = payload.Deserialize<AgentEvent>(JsonOptions)!;
        if (update.Status is not ("started" or "completed" or "failed" or "interrupted" or "unknown"))
            throw new JsonException("Invalid agent event");
        update = update with
        {
            Output = update.Output?[..Math.Min(update.Output.Length, 16000)],
            Error = update.Error?[..Math.Min(update.Error.Length, 400)]
        };
        if (update.Status != "started" && !_pendingRequests.TryRemove(id, out _)) return;
        await sink.PublishAsync(pending.Destination, update);
    }
}
