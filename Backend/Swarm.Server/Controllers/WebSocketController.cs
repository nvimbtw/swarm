using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;
using System.Text;

namespace Swarm.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class WebSocketController : ControllerBase
{
    private static async Task Echo(WebSocket webSocket)
    {
        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                var message = Encoding.UTF8.GetBytes("Hello from Swarm!");

                await webSocket.SendAsync(
                    new ArraySegment<byte>(message),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);

                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
        catch (WebSocketException)
        {
            Console.WriteLine("Client disconnected.");
        }
    }

    [Route("/ws")]
    public async Task Get()
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            await Echo(webSocket);
        }
        else
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
}
