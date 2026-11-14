using Swarm.Server.Classi;
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
