using System.Text.Json;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Swarm.Server.Connections;

namespace DiscordBot.Commands;

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
    protected abstract IEnumerable<string> Options(JsonElement data,
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
    protected override IEnumerable<string> Options(JsonElement data,
        IReadOnlyList<ApplicationCommandInteractionDataOption> options) =>
        data.GetProperty("models").EnumerateArray().Select(m => m.GetProperty("id").GetString()!);
}

public sealed class AgentReasoningAutocomplete(AgentRouter agents) : AgentOptionsAutocomplete(agents)
{
    protected override IEnumerable<string> Options(JsonElement data,
        IReadOnlyList<ApplicationCommandInteractionDataOption> options)
    {
        var model = options.FirstOrDefault(x => x.Name == "model")?.Value ?? data.GetProperty("model").GetString();
        return data.GetProperty("models").EnumerateArray()
            .Where(m => m.GetProperty("id").GetString() == model)
            .SelectMany(m => m.GetProperty("reasoning").EnumerateArray()).Select(r => r.GetString()!);
    }
}
