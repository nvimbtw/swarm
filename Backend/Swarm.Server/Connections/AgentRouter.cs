using System.Collections.Concurrent;
using System.Text.Json;

namespace Swarm.Server.Connections;

public sealed class AgentRequestException(string code) : Exception(code);
public sealed record AgentRequest(Guid SessionId, string Prompt, string Delivery = "queue",
    string? Model = null, string? Reasoning = null);
public sealed record AgentReply(Guid RequestId, string Status, string? Error = null);
public sealed record AgentEvent(Guid RequestId, Guid SessionId, string Status,
    string? Output = null, string? Error = null, string? TurnId = null);
public sealed record AgentDestination(string SenderId, ulong ChannelId);
public interface IAgentEventSink
{
    ValueTask PublishAsync(AgentDestination destination, AgentEvent update);
}

/// <summary>Multiplexes correlated requests/events over the registered installation's socket.</summary>
public sealed class AgentRouter(SessionRegistry registry, IAgentEventSink sink)
{
    private sealed record Transport(Func<object, CancellationToken, Task> Send, Func<bool> Authenticated);
    private sealed record Pending(Guid ConnectionId, Guid SessionId, AgentDestination Destination,
        TaskCompletionSource<JsonElement> Reply, DateTimeOffset CreatedAt);
    private readonly ConcurrentDictionary<Guid, Transport> _connections = new();
    private readonly ConcurrentDictionary<Guid, Pending> _requests = new();
    private readonly SemaphoreSlim _admission = new(1, 1);

    public void Connect(Guid id, Func<object, CancellationToken, Task> send, Func<bool> authenticated) =>
        _connections[id] = new(send, authenticated);

    public async Task DisconnectAsync(Guid connectionId)
    {
        _connections.TryRemove(connectionId, out _);
        foreach (var pair in _requests.Where(x => x.Value.ConnectionId == connectionId).ToArray())
            if (_requests.TryRemove(pair.Key, out var pending))
            {
                pending.Reply.TrySetException(new AgentRequestException("connection_lost_delivery_unknown"));
                await sink.PublishAsync(pending.Destination, new(pair.Key, pending.SessionId, "unknown",
                    Error: "Connection lost. Check local Codex before resending; accepted work is not replayed."));
            }
    }

    public async Task<JsonElement> DescribeAsync(string senderId, Guid sessionId, CancellationToken ct = default) =>
        await Exchange(senderId, sessionId, "agent.describe", null, new(senderId, 0), ct);

    public async Task<AgentReply> SubmitAsync(AgentDestination destination, AgentRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt) || request.Prompt.Length > 4000 ||
            request.Delivery is not ("queue" or "inject") || !Optional(request.Model, 100) ||
            !Optional(request.Reasoning, 40)) throw new AgentRequestException("invalid_request");
        var result = await Exchange(destination.SenderId, request.SessionId, "agent.submit", request, destination, ct);
        return new(result.GetProperty("requestId").GetGuid(), result.GetProperty("status").GetString()!,
            result.TryGetProperty("error", out var error) ? error.GetString() : null);
    }

    private static bool Optional(string? value, int max) => value is null ||
        (!string.IsNullOrWhiteSpace(value) && value.Length <= max && !value.Any(char.IsControl));

    private async Task<JsonElement> Exchange(string senderId, Guid sessionId, string type,
        AgentRequest? request, AgentDestination destination, CancellationToken ct)
    {
        var session = registry.FindAvailable(senderId, sessionId)
            ?? throw new AgentRequestException("session_unavailable_or_not_allowed");
        if (!_connections.TryGetValue(session.ConnectionId, out var transport) || !transport.Authenticated())
            throw new AgentRequestException("session_offline");
        var requestId = Guid.NewGuid();
        var pending = new Pending(session.ConnectionId, sessionId, destination,
            new(TaskCreationOptions.RunContinuationsAsynchronously), DateTimeOffset.UtcNow);
        await _admission.WaitAsync(ct);
        try
        {
            // Bound outstanding work as well as transient autocomplete operations.
            foreach (var old in _requests.Where(x => x.Value.CreatedAt < DateTimeOffset.UtcNow.AddHours(-24)).ToArray())
                if (_requests.TryRemove(old.Key, out var expired))
                    expired.Reply.TrySetException(new AgentRequestException("request_expired"));
            if (_requests.Count >= 1000 || _requests.Values.Count(x => x.ConnectionId == session.ConnectionId) >= 100)
                throw new AgentRequestException("installation_busy");
            _requests[requestId] = pending;
        }
        finally { _admission.Release(); }
        bool keep = false;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(12));
            await transport.Send(new { version = 1, id = requestId, type, payload = new {
                requestId, sessionId, senderId, channelId = destination.ChannelId.ToString(),
                prompt = request?.Prompt, delivery = request?.Delivery, model = request?.Model,
                reasoning = request?.Reasoning } }, timeout.Token);
            var response = await pending.Reply.Task.WaitAsync(timeout.Token);
            var status = response.GetProperty("status").GetString();
            keep = type == "agent.submit" && (status is "queued" or "injected");
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
            if (!keep) _requests.TryRemove(requestId, out _);
        }
    }

    public async Task AcceptAsync(Guid connectionId, string type, JsonElement payload)
    {
        var id = payload.GetProperty("requestId").GetGuid();
        var sessionId = payload.GetProperty("sessionId").GetGuid();
        if (!_requests.TryGetValue(id, out var pending) || pending.ConnectionId != connectionId ||
            pending.SessionId != sessionId) return; // Never trust client-supplied routing destinations.
        if (type == "agent.reply")
        {
            pending.Reply.TrySetResult(payload.Clone());
            return;
        }
        var update = payload.Deserialize<AgentEvent>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        if (update.Status is not ("started" or "completed" or "failed" or "interrupted" or "unknown"))
            throw new JsonException("Invalid agent event");
        update = update with { Output = update.Output?[..Math.Min(update.Output.Length, 16000)],
            Error = update.Error?[..Math.Min(update.Error.Length, 400)] };
        if (update.Status != "started") _requests.TryRemove(id, out _);
        await sink.PublishAsync(pending.Destination, update);
    }
}
