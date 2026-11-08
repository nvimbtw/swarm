using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Swarm.Server.Connections;

namespace DiscordBot.Commands;

public enum AgentDelivery { Queue, Inject }

public sealed class AgentCommand(AgentRouter agents) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("agent", "Send a prompt to a connected coding session")]
    public async Task Execute(
        [SlashCommandParameter(Name = "session", Description = "Connected harness session",
            AutocompleteProviderType = typeof(AgentSessionAutocomplete))] string session,
        [SlashCommandParameter(Name = "prompt", Description = "Prompt to send to this session",
            MinLength = 1, MaxLength = 4000)] string prompt,
        [SlashCommandParameter(Name = "model", Description = "Model for this and subsequent queued turns",
            AutocompleteProviderType = typeof(AgentModelAutocomplete), MaxLength = 100)] string? model = null,
        [SlashCommandParameter(Name = "reasoning", Description = "Reasoning level supported by the selected model",
            AutocompleteProviderType = typeof(AgentReasoningAutocomplete), MaxLength = 40)] string? reasoning = null,
        [SlashCommandParameter(Name = "delivery", Description = "Queue by default; inject steers an active turn")]
            AgentDelivery delivery = AgentDelivery.Queue)
    {
        await RespondAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));
        string content;
        try
        {
            if (!Guid.TryParse(session, out var sessionId)) throw new AgentRequestException("choose_a_connected_session");
            var result = await agents.SubmitAsync(new(Context.User.Id.ToString(), Context.Channel.Id),
                new(sessionId, prompt, delivery == AgentDelivery.Inject ? "inject" : "queue", model, reasoning));
            content = result.Status switch
            {
                "queued" => $"Queued on the local connector. Request `{result.RequestId}`. " +
                    "The result will be posted in this channel. Approvals stay in local Codex.",
                "injected" => $"Injected into the active Codex turn. Request `{result.RequestId}`. " +
                    "The active turn’s result will be posted in this channel.",
                _ => $"Request `{result.RequestId}`: {result.Status}. {result.Error}"
            };
        }
        catch (AgentRequestException error) { content = $"Could not confirm delivery: {error.Message}"; }
        await ModifyResponseAsync(message =>
        {
            message.Content = content;
            message.AllowedMentions = AllowedMentionsProperties.None;
        });
    }
}

public sealed class AgentSessionAutocomplete(SessionRegistry registry) : IAutocompleteProvider<AutocompleteInteractionContext>
{
    public ValueTask<IEnumerable<ApplicationCommandOptionChoiceProperties>?> GetChoicesAsync(
        ApplicationCommandInteractionDataOption option, AutocompleteInteractionContext context)
    {
        var choices = registry.Available(context.User.Id.ToString())
            .Select(s => new { Session = s, Label = $"{s.Name} · {s.Harness} · {s.Identity.MachineName} · {s.ProjectId}" })
            .Where(s => s.Label.Contains(option.Value ?? "", StringComparison.OrdinalIgnoreCase))
            .Take(25).Select(s => new ApplicationCommandOptionChoiceProperties(
                s.Label[..Math.Min(100, s.Label.Length)], s.Session.SessionId.ToString()));
        return new(choices);
    }
}

public abstract class AgentOptionsAutocomplete(AgentRouter agents) : IAutocompleteProvider<AutocompleteInteractionContext>
{
    protected abstract IEnumerable<string> Options(System.Text.Json.JsonElement data,
        IReadOnlyList<ApplicationCommandInteractionDataOption> options);

    public async ValueTask<IEnumerable<ApplicationCommandOptionChoiceProperties>?> GetChoicesAsync(
        ApplicationCommandInteractionDataOption option, AutocompleteInteractionContext context)
    {
        var options = context.Interaction.Data.Options;
        var selected = options.FirstOrDefault(x => x.Name == "session")?.Value;
        if (!Guid.TryParse(selected, out var sessionId)) return [];
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var data = await agents.DescribeAsync(context.User.Id.ToString(), sessionId, timeout.Token);
            if (!data.TryGetProperty("models", out _)) return [];
            return Options(data, options).Where(x => x.Contains(option.Value ?? "", StringComparison.OrdinalIgnoreCase))
                .Where(x => x.Length is > 0 and <= 100).Distinct().Take(25)
                .Select(x => new ApplicationCommandOptionChoiceProperties(x, x)).ToArray();
        }
        catch (AgentRequestException) { return []; }
    }
}

public sealed class AgentModelAutocomplete(AgentRouter agents) : AgentOptionsAutocomplete(agents)
{
    protected override IEnumerable<string> Options(System.Text.Json.JsonElement data,
        IReadOnlyList<ApplicationCommandInteractionDataOption> options) =>
        data.GetProperty("models").EnumerateArray().Select(m => m.GetProperty("id").GetString()!);
}

public sealed class AgentReasoningAutocomplete(AgentRouter agents) : AgentOptionsAutocomplete(agents)
{
    protected override IEnumerable<string> Options(System.Text.Json.JsonElement data,
        IReadOnlyList<ApplicationCommandInteractionDataOption> options)
    {
        var model = options.FirstOrDefault(x => x.Name == "model")?.Value ?? data.GetProperty("model").GetString();
        return data.GetProperty("models").EnumerateArray()
            .Where(m => m.GetProperty("id").GetString() == model)
            .SelectMany(m => m.GetProperty("reasoning").EnumerateArray()).Select(r => r.GetString()!);
    }
}
