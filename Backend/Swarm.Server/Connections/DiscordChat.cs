using System.Text.Json;

namespace Swarm.Server.Connections;

public sealed class ChatException(string code) : Exception(code);
public sealed record ChatRequest(Guid SessionId, string ChannelId, string? Content = null,
    string? ReplyToMessageId = null, int Limit = 20, string? BeforeMessageId = null);
public sealed record ChatChannel(string Id, string GuildId, string Name, string? ParentId, bool IsThread);
public sealed record ChatAttachment(string Name, string Url);
public sealed record ChatMessage(string Id, string AuthorId, string AuthorName, bool IsBot,
    string Content, DateTimeOffset CreatedAt, string? ReplyToMessageId, ChatAttachment[] Attachments,
    string? Footer = null);
public sealed record ChatPage(ChatChannel Channel, ChatMessage[] Messages, string? NextBeforeMessageId);
public sealed record ChatSent(string ChannelId, string MessageId, string Url);

/// <summary>Discord access is checked for the paired owner as well as the bot on every call.</summary>
public interface IDiscordChat
{
    Task<ChatChannel> GetChannelAsync(string ownerId, ulong channelId, bool write, CancellationToken ct);
    Task<ChatPage> ReadAsync(ChatChannel channel, int limit, ulong? before, CancellationToken ct);
    Task<ChatSent> SendAsync(ChatChannel channel, string content, ulong? replyTo,
        AgentAttribution attribution, CancellationToken ct);
}

public sealed class DiscordChatService(SessionRegistry sessions, IDiscordChat discord)
{
    public async Task<object> ExecuteAsync(Guid connectionId, string kind, ChatRequest request, CancellationToken ct)
    {
        var session = sessions.FindConnected(connectionId, request.SessionId)
            ?? throw new ChatException("session_unavailable_or_not_allowed");
        var channelId = ParseId(request.ChannelId);
        bool write = kind == "discord.send";
        if (kind is not ("discord.channel" or "discord.read" or "discord.send"))
            throw new ChatException("unknown_message_type");
        if (request.Limit is < 1 or > 50 || (write &&
            (string.IsNullOrWhiteSpace(request.Content) || request.Content.Length > 1800)))
            throw new ChatException("invalid_request");
        ulong? before = request.BeforeMessageId is null ? null : ParseId(request.BeforeMessageId);
        ulong? replyTo = request.ReplyToMessageId is null ? null : ParseId(request.ReplyToMessageId);
        var channel = await discord.GetChannelAsync(session.Identity.OwnerId, channelId, write, ct);
        // Authorization may involve several REST calls. Recheck attachment ownership before acting.
        if (sessions.FindConnected(connectionId, request.SessionId) != session)
            throw new ChatException("session_unavailable_or_not_allowed");
        if (kind == "discord.channel") return channel;
        if (kind == "discord.read") return await discord.ReadAsync(channel, request.Limit, before, ct);
        return await discord.SendAsync(channel, request.Content!, replyTo, AgentAttribution.FromSession(session), ct);
    }

    private static ulong ParseId(string value) => value is { Length: > 0 and <= 20 } &&
        value.All(char.IsAsciiDigit) && ulong.TryParse(value, out var id) && id != 0
            ? id : throw new ChatException("invalid_discord_id");

    // Leave room for the response envelope within the shared 64 KiB socket frame.
    public static bool FitsPage(IReadOnlyList<ChatMessage> messages) =>
        JsonSerializer.SerializeToUtf8Bytes(messages, new JsonSerializerOptions(JsonSerializerDefaults.Web)).Length < 54 * 1024;
}
