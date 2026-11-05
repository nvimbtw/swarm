
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordBot.Commands;

public class SetupProject : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("setup-project", "Setup a Discord server for future development")]
    public async Task<string> Execute(string ProjectName)
    {
        if(Context.Guild == null)
        {
            return "This command can only be used inside a Discord server!";
        }

        ulong guildId = Context.Guild.Id;

        // Ustvari kategorijo
        var category = await Context.Client.Rest.CreateGuildChannelAsync(
            guildId,
            new GuildChannelProperties(ProjectName, ChannelType.CategoryChannel)
        );

        // Seznam kanalov
        string[] channels = {
            "issues",
            "features",
            "prs",
            "docs",
            "stats"
        };

        // Ustvari kanale znotraj kategorije
        foreach(string channelName in channels)
        {
            var channel = new GuildChannelProperties(
                channelName,
                ChannelType.TextGuildChannel
            );

            channel.ParentId = category.Id;

            await Context.Client.Rest.CreateGuildChannelAsync(guildId, channel);
        }

        return $"Project **{ProjectName}** has been successfully created!";
    }
}
