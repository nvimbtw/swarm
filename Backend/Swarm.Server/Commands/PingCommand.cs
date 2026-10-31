using NetCord.Services.ApplicationCommands;

namespace DiscordBot.Commands;

public class PingCommand : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("ping", "Check if the bot is online")]
    public string Execute()
    {
        return "Helloooo ping is working";
    }
}