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
