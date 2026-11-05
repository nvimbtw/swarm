using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordBot.Commands;

public sealed class SetupRolesCommand(RestClient rest) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("setup-roles", "Create the default Swarm server roles")]
    public async Task Execute()
    {
        if(Context.Interaction.GuildId is null)
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Content = "This command can only be used inside a server.",
                Flags = MessageFlags.Ephemeral
            }));

            return;
        }

        ulong guildId = Context.Interaction.GuildId.Value;

        var guild = await rest.GetGuildAsync(guildId);
        var roles = await rest.GetGuildRolesAsync(guildId);

        string[] roleNames = { "Developer", "Moderator", "Admin", "Owner" };

        int created = 0;
        ulong ownerRoleId = 0;

        foreach(string roleName in roleNames)
        {
            Role? foundRole = null;

            foreach(var role in roles)
            {
                if(role.Name == roleName)
                {
                    foundRole = role;
                    break;
                }
            }

            if(foundRole is null)
            {
                foundRole = await rest.CreateGuildRoleAsync(guildId, new RoleProperties
                {
                    Name = roleName,
                    Hoist = true,
                    Mentionable = false
                });

                created++;
            }

            if(roleName == "Owner")
            {
                ownerRoleId = foundRole.Id;
            }
        }

        // Assign Owner role to actual server owner
        await rest.AddGuildUserRoleAsync(guildId, guild.OwnerId, ownerRoleId);

        await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
        {
            Content =
                "**Swarm roles setup completed!**\n\n" +
                $"Created roles: **{created}**\n" +
                $"Server owner: <@{guild.OwnerId}>\n" +
                "Automatically assigned: **Owner**",
            Flags = MessageFlags.Ephemeral
        }));
    }
}