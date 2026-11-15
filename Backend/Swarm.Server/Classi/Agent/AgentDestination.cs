using Swarm.Server.Connections;

namespace Swarm.Server.Classi;

public sealed record AgentDestination(string SenderId, ulong ChannelId, AgentAttribution? Agent = null,
    string? Prompt = null, ulong? PromptMessageId = null);
