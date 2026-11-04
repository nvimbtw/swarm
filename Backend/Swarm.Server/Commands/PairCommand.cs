using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Swarm.Server.Connections;

namespace DiscordBot.Commands;

public sealed class PairCommand(PairingService pairing) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("swarm-pair", "Pair a local Swarm installation with your Discord account")]
    public Task Execute([SlashCommandParameter(Name = "pairing-id", Description = "Pairing ID shown by your local connector",
        MinLength = 32, MaxLength = 32)] string pairingId)
    {
        var result = pairing.Approve(pairingId, Context.User.Id.ToString());
        string content = result is null
            ? "Pairing was not found, expired, or already claimed. Start pairing again locally."
            : $"Pairing machine: `{result.MachineName.Replace("`", "'")}`\n" +
              $"Enter this code in your local agent chat: **{result.Code}**\n" +
              "Only enter it if you initiated this pairing. It expires within five minutes.";
        return RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
        {
            Content = content,
            Flags = MessageFlags.Ephemeral,
            AllowedMentions = AllowedMentionsProperties.None
        }));
    }
}
