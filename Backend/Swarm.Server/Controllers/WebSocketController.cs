using Microsoft.AspNetCore.Mvc;
using Swarm.Server.Diagnostics;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;

namespace Swarm.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class WebSocketController(
    ConnectionDiagnostics diagnostics,
    IHostApplicationLifetime applicationLifetime,
    ILogger<WebSocketController> logger) : ControllerBase
{
    private async Task SendGreetingsAsync(
        WebSocket webSocket, Guid connectionId, CancellationToken cancellationToken)
    {
        byte[] message = Encoding.UTF8.GetBytes("Hello from Swarm!");

        while (webSocket.State == WebSocketState.Open)
        {
            long startedAt = Stopwatch.GetTimestamp();
            await webSocket.SendAsync(
                new ArraySegment<byte>(message),
                WebSocketMessageType.Text,
                true,
                cancellationToken);

            // Time the server write, not a round trip to the client.
            double milliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            diagnostics.RecordSent(connectionId, milliseconds);
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }

    private async Task ReceiveMessagesAsync(
        WebSocket webSocket, Guid connectionId, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        while (webSocket.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer), cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return;
            }

            // Count complete application messages rather than individual fragments.
            // Payloads are discarded until Swarm's message protocol is implemented.
            if (result.EndOfMessage)
            {
                diagnostics.RecordReceived(connectionId);
            }
        }
    }

    [HttpGet("/ws")]
    public async Task Get()
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        Guid connectionId = diagnostics.OpenConnection();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            HttpContext.RequestAborted, applicationLifetime.ApplicationStopping);

        try
        {
            // WebSocket supports one send and one receive concurrently.
            Task sender = SendGreetingsAsync(webSocket, connectionId, cancellation.Token);
            Task receiver = ReceiveMessagesAsync(webSocket, connectionId, cancellation.Token);
            await Task.WhenAny(sender, receiver);
            cancellation.Cancel();

            // Observe both tasks before disposing the socket or removing its diagnostics.
            try
            {
                await Task.WhenAll(sender, receiver);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                // Expected when either loop finishes or the application shuts down.
            }

            if (webSocket.State == WebSocketState.CloseReceived)
            {
                using var closeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await webSocket.CloseOutputAsync(
                        WebSocketCloseStatus.NormalClosure, "Connection closed.", closeTimeout.Token);
                }
                catch (OperationCanceledException) when (closeTimeout.IsCancellationRequested)
                {
                    logger.LogDebug("Closing WebSocket {ConnectionId} timed out.", connectionId);
                }
            }
        }
        catch (WebSocketException exception)
        {
            logger.LogDebug(exception, "WebSocket {ConnectionId} disconnected.", connectionId);
        }
        finally
        {
            cancellation.Cancel();
            diagnostics.CloseConnection(connectionId);
        }
    }
}
