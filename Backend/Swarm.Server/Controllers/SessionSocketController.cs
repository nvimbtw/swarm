using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Swarm.Server.Connections;
using Swarm.Server.Diagnostics;

namespace Swarm.Server.Controllers;

[ApiController]
public sealed class SessionSocketController(PairingService pairing, SessionRegistry registry, AgentRouter agents,
    ConnectionDiagnostics diagnostics, IHostApplicationLifetime lifetime,
    ILogger<SessionSocketController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpGet("/ws/sessions")]
    public async Task Connect()
    {
        string authorization = Request.Headers.Authorization.ToString();
        var identity = authorization.StartsWith("Bearer ", StringComparison.Ordinal)
            ? pairing.Authenticate(authorization[7..]) : null;
        if (identity is null) { Response.StatusCode = 401; return; }
        if (!HttpContext.WebSockets.IsWebSocketRequest) { Response.StatusCode = 400; return; }

        using var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            HttpContext.RequestAborted, lifetime.ApplicationStopping);
        var ct = cancellation.Token;
        Guid connectionId = diagnostics.OpenConnection();
        // Fixed limit across fragmented frames, not just each individual frame.
        var buffer = new byte[64 * 1024 + 1];
        using var sendGate = new SemaphoreSlim(1, 1);
        agents.Connect(connectionId, SendCore, () => pairing.Authenticate(authorization[7..]) is not null);
        try
        {
            await Send(new { type = "hello", version = 1, ownerId = identity.OwnerId });
            while (socket.State == WebSocketState.Open)
            {
                int count = 0;
                WebSocketReceiveResult received;
                do
                {
                    received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, count, buffer.Length - count), ct);
                    if (received.MessageType == WebSocketMessageType.Close)
                    {
                        await Close(WebSocketCloseStatus.NormalClosure, "Disconnected");
                        return;
                    }
                    count += received.Count;
                    if (count > 64 * 1024 || received.MessageType != WebSocketMessageType.Text)
                    {
                        await Close(WebSocketCloseStatus.MessageTooBig, "Expected JSON text up to 64 KiB");
                        return;
                    }
                } while (!received.EndOfMessage);
                diagnostics.RecordReceived(connectionId);
                if (pairing.Authenticate(authorization[7..]) is null)
                {
                    await Close(WebSocketCloseStatus.PolicyViolation, "Credential replaced; reconnect");
                    return;
                }
                string? requestId = null;
                try
                {
                    using var document = JsonDocument.Parse(buffer.AsMemory(0, count));
                    var message = document.RootElement;
                    requestId = message.GetProperty("id").GetString();
                    if (!PairingController.Valid(requestId, 100) || message.GetProperty("version").GetInt32() != 1)
                        throw new JsonException("Invalid envelope");
                    switch (message.GetProperty("type").GetString())
                    {
                        case "agent.reply":
                        case "agent.event":
                            await agents.AcceptAsync(connectionId, message.GetProperty("type").GetString()!,
                                message.GetProperty("payload"));
                            break;
                        case "session.register":
                            var registration = message.GetProperty("payload").Deserialize<SessionRegistration>(JsonOptions);
                            if (registration is null || registration.SessionId == Guid.Empty ||
                                !PairingController.Valid(registration.Name, 80) ||
                                !PairingController.Valid(registration.ProjectId, 100) ||
                                !PairingController.Valid(registration.Harness, 40) ||
                                registration.Mode is not ("solo" or "swarm")) throw new JsonException("Invalid session");
                            await Send(new
                            {
                                id = requestId,
                                type = "session.registered",
                                payload = registry.Register(connectionId, identity, registration)
                            });
                            break;
                        case "session.unregister":
                            Guid sessionId = message.GetProperty("payload").GetProperty("sessionId").GetGuid();
                            await Send(new
                            {
                                id = requestId,
                                type = "session.unregistered",
                                removed = registry.Remove(connectionId, sessionId)
                            });
                            break;
                        case "session.list":
                            await Send(new { id = requestId, type = "session.listed", payload = registry.List(identity.OwnerId) });
                            break;
                        case "ping":
                            await Send(new { id = requestId, type = "pong" });
                            break;
                        default:
                            await Send(new { id = requestId, type = "error", error = "unknown_message_type" });
                            break;
                    }
                }
                catch (Exception exception) when (exception is JsonException or InvalidOperationException or
                    KeyNotFoundException or FormatException)
                {
                    await Send(new { id = requestId, type = "error", error = "invalid_request" });
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (WebSocketException exception) { logger.LogDebug(exception, "Session connection ended."); }
        finally
        {
            registry.Disconnect(connectionId);
            await agents.DisconnectAsync(connectionId);
            diagnostics.CloseConnection(connectionId);
        }

        Task Send(object message) => SendCore(message, ct);

        async Task SendCore(object message, CancellationToken token)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct, token);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            await sendGate.WaitAsync(timeout.Token);
            try
            {
                var started = Stopwatch.GetTimestamp();
                await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions),
                    WebSocketMessageType.Text, true, timeout.Token);
                diagnostics.RecordSent(connectionId, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
            finally { sendGate.Release(); }
        }

        async Task Close(WebSocketCloseStatus status, string reason)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            await sendGate.WaitAsync(timeout.Token);
            try { await socket.CloseOutputAsync(status, reason, timeout.Token); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { socket.Abort(); }
            finally { sendGate.Release(); }
        }
    }
}
