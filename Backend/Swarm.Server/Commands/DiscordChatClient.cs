using NetCord;
using NetCord.Rest;
using Swarm.Server.Connections;

namespace DiscordBot.Commands;

public sealed class DiscordChatClient(RestClient rest) : IDiscordChat
{
    public async Task<ChatChannel> GetChannelAsync(string ownerId, ulong channelId, bool write, CancellationToken ct)
    {
        if (!ulong.TryParse(ownerId, out var userId)) throw new ChatException("discord_access_denied");
        var channel = await rest.GetChannelAsync(channelId, cancellationToken: ct);
        if (channel is not TextGuildChannel text) throw new ChatException("unsupported_discord_channel");
        var guild = await rest.GetGuildAsync(text.GuildId, cancellationToken: ct);
        var member = await rest.GetGuildUserAsync(text.GuildId, userId, cancellationToken: ct);
        IGuildChannel permissionsChannel = text;
        var thread = text as GuildThread;
        if (thread is not null)
            permissionsChannel = await rest.GetChannelAsync(thread.ParentId, cancellationToken: ct) as IGuildChannel
                ?? throw new ChatException("discord_access_denied");
        var permissions = member.GetChannelPermissions(guild, permissionsChannel);
        bool administrator = guild.OwnerId == userId || permissions.HasFlag(Permissions.Administrator);
        var required = Permissions.ViewChannel | (write
            ? thread is null ? Permissions.SendMessages : Permissions.SendMessagesInThreads
            : Permissions.ReadMessageHistory);
        if ((!administrator && (permissions & required) != required) ||
            (write && (member.TimeOutUntil > DateTimeOffset.UtcNow || member.IsPending == true)))
            throw new ChatException("discord_access_denied");
        if (thread is PrivateGuildThread && !administrator && !permissions.HasFlag(Permissions.ManageThreads))
            await thread.GetUserAsync(userId, cancellationToken: ct); // Membership is separate from parent visibility.
        if (write && thread is not null && (thread.Metadata.Archived || thread.Metadata.Locked))
            throw new ChatException("discord_thread_archived_or_locked");
        return new(channelId.ToString(), text.GuildId.ToString(), text.Name,
            text.ParentId?.ToString(), thread is not null);
    }

    public async Task<ChatPage> ReadAsync(ChatChannel channel, int limit, ulong? before, CancellationToken ct)
    {
        var messages = new List<ChatMessage>();
        bool more = false;
        await foreach (var message in rest.GetMessagesAsync(ulong.Parse(channel.Id), new()
        { From = before, Direction = PaginationDirection.Before, BatchSize = limit + 1 }).WithCancellation(ct))
        {
            if (messages.Count == limit) { more = true; break; }
            var item = new ChatMessage(message.Id.ToString(), message.Author.Id.ToString(),
                message.Author.GlobalName ?? message.Author.Username, message.Author.IsBot,
                message.Content, message.CreatedAt, message.MessageReference?.MessageId.ToString(),
                message.Attachments.Take(5).Select(a => new ChatAttachment(a.FileName, a.Url)).ToArray(),
                message.Embeds.Select(e => e.Footer?.Text).FirstOrDefault(text => text is not null));
            messages.Add(item);
            if (!DiscordChatService.FitsPage(messages))
            {
                messages.RemoveAt(messages.Count - 1);
                if (messages.Count == 0) throw new ChatException("discord_message_too_large");
                more = true;
                break;
            }
        }
        // Discord returns newest first. The cursor always refers to the oldest included message.
        var cursor = more ? messages[^1].Id : null;
        messages.Reverse();
        return new(channel, messages.ToArray(), cursor);
    }

    public async Task<ChatSent> SendAsync(ChatChannel channel, string content, ulong? replyTo,
        AgentAttribution attribution, CancellationToken ct)
    {
        var channelId = ulong.Parse(channel.Id);
        var context = await AgentMessagePresentation.ResolveAsync(rest, attribution, channelId, ct, channel);
        var properties = AgentMessagePresentation.Create(content, attribution, channelId, context);
        properties.MessageReference = replyTo is { } id ? MessageReferenceProperties.Reply(id, failIfNotExists: true) : null;
        var message = await rest.SendMessageAsync(channelId, properties, cancellationToken: ct);
        return new(channel.Id, message.Id.ToString(),
            $"https://discord.com/channels/{channel.GuildId}/{channel.Id}/{message.Id}");
    }
}
