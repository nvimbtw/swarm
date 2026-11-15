using System.Collections.Concurrent;
using Swarm.Server.Connections;

sealed class TestDiscordChat : IDiscordChat
{
    public ConcurrentQueue<SentChat> Sent { get; } = new();
    public Task<ChatChannel> GetChannelAsync(string ownerId, ulong channelId, bool write, CancellationToken ct)
    {
        if (ownerId is not ("owner" or "discord-test-owner") || channelId is not (123 or 456))
            throw new ChatException("discord_access_denied");
        return Task.FromResult(new ChatChannel(channelId.ToString(), "1", channelId == 456 ? "thread" : "general",
            channelId == 456 ? "123" : null, channelId == 456));
    }

    public async Task<ChatPage> ReadAsync(ChatChannel channel, int limit, ulong? before, CancellationToken ct)
    {
        await Task.Delay(150, ct); // Exercise multiplexing while Discord is slower than the socket.
        var messages = Enumerable.Range(1, 5).Select(i => new ChatMessage(i.ToString(), "99", "Person", false,
            $"Message {i} 🌍", DateTimeOffset.UtcNow, null, Array.Empty<ChatAttachment>()))
            .Where(m => before is null || ulong.Parse(m.Id) < before).TakeLast(limit).ToArray();
        return new(channel, messages, messages.Length != 0 && messages[0].Id != "1" ? messages[0].Id : null);
    }

    public Task<ChatSent> SendAsync(ChatChannel channel, string content, ulong? replyTo,
        AgentAttribution attribution, CancellationToken ct)
    {
        Sent.Enqueue(new(channel.Id, content, replyTo?.ToString(), attribution));
        return Task.FromResult(new ChatSent(channel.Id, "900", $"https://discord.com/channels/1/{channel.Id}/900"));
    }
}

sealed record SentChat(string ChannelId, string Content, string? ReplyTo, AgentAttribution Attribution);
