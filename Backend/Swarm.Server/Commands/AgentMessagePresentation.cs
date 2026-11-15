using Swarm.Server.Classi;
using NetCord;
using NetCord.Rest;
using Swarm.Server.Connections;

namespace DiscordBot.Commands;

/// <summary>One footer format for explicit MCP posts and automatic request results.</summary>
public static class AgentMessagePresentation
{
    public sealed record Context(string OwnerName, string ChannelName, bool? IsThread);

    public static async Task<Context> ResolveAsync(RestClient rest, AgentAttribution agent, ulong channelId,
        CancellationToken ct, ChatChannel? knownChannel = null)
    {
        string ownerName = agent.OwnerId;
        string channelName = knownChannel?.Name ?? channelId.ToString();
        bool? isThread = knownChannel?.IsThread;
        // Names are for readability; immutable IDs below remain authoritative if lookups fail.
        try
        {
            if (ulong.TryParse(agent.OwnerId, out var ownerId))
            {
                var owner = await rest.GetUserAsync(ownerId, cancellationToken: ct);
                ownerName = owner.GlobalName is { Length: > 0 } name
                    ? $"{name} (@{owner.Username})" : $"@{owner.Username}";
            }
        }
        catch (Exception error) when (error is RestException or HttpRequestException) { }
        if (knownChannel is null)
        {
            try
            {
                if (await rest.GetChannelAsync(channelId, cancellationToken: ct) is TextGuildChannel channel)
                {
                    channelName = channel.Name;
                    isThread = channel is GuildThread;
                }
            }
            catch (Exception error) when (error is RestException or HttpRequestException) { }
        }
        return new(ownerName, channelName, isThread);
    }

    public static MessageProperties Create(string content, AgentAttribution agent, ulong channelId,
        Context context, Guid? requestId = null, string? status = null, int part = 1, int parts = 1,
        string? prompt = null)
    {
        string location = context.IsThread switch { true => "Thread", false => "Channel", null => "Channel/thread" };
        string footer = $"Agent: {Label(agent.AgentName, 80)} · Owner: {Label(context.OwnerName, 100)} ({agent.OwnerId})\n" +
            $"Harness: {Label(agent.Harness, 40)} · Project: {Label(agent.ProjectId, 100)} · Machine: {Label(agent.MachineName, 80)} ({agent.MachineId})\n" +
            $"Session: {agent.SessionId} · {location}: #{Label(context.ChannelName, 100)} ({channelId})";
        if (requestId is not null) footer += $"\nRequest: {requestId} · Status: {Label(status ?? "unknown", 40)}";
        if (parts > 1) footer += $" · Part {part}/{parts}";
        return new MessageProperties
        {
            Content = content,
            AllowedMentions = AllowedMentionsProperties.None,
            Embeds = [new EmbedProperties
            {
                Fields = prompt is null ? null : [new EmbedFieldProperties { Name = "In response to", Value = Excerpt(prompt, 800) }],
                Footer = new EmbedFooterProperties { Text = footer }
            }]
        };
    }

    public static EmbedProperties PromptReceipt(string agentName, string prompt) => new()
    {
        Title = $"Request to {Label(agentName, 80)}",
        Description = prompt
    };

    public static string ResultText(AgentEvent update)
    {
        var content = string.IsNullOrWhiteSpace(update.Output) ? update.Status switch
        {
            "completed" => "The agent completed this request.",
            "interrupted" => "The agent was interrupted before completing this request.",
            "unknown" => "Swarm could not confirm delivery. The agent may still be working on this request.",
            _ => "The agent could not complete this request."
        } : update.Output;
        if (!string.IsNullOrEmpty(update.Error)) content += "\n\n" + (update.Error switch
        {
            "native_queue_check_local_codex" or "native_queue_check_local_codex_start" =>
                "Codex accepted the queued prompt, but Swarm could not identify its turn. Check the local conversation before resending it.",
            "delivery_unknown" or "codex_connection_lost" =>
                "The connection was lost before confirmation. Check the local conversation before resending this prompt.",
            _ => update.Error
        });
        return content;
    }

    private static string Excerpt(string value, int max)
    {
        if (value.Length <= max) return value;
        int length = max - 1;
        if (char.IsHighSurrogate(value[length - 1])) length--;
        return value[..length] + "…";
    }

    public static IReadOnlyList<string> Split(string content)
    {
        var parts = new List<string>();
        for (int offset = 0; offset < content.Length;)
        {
            int length = Math.Min(1800, content.Length - offset);
            if (offset + length < content.Length && char.IsHighSurrogate(content[offset + length - 1])) length--;
            parts.Add(content.Substring(offset, length));
            offset += length;
        }
        return parts;
    }

    private static string Label(string value, int max)
    {
        var text = string.Concat(value.Where(c => !char.IsControl(c)));
        int length = Math.Min(text.Length, max);
        if (length > 0 && length < text.Length && char.IsHighSurrogate(text[length - 1])) length--;
        return text[..length];
    }
}
