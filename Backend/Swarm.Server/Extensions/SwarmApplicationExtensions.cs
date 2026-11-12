
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;
using Swarm.Server.Db;
using Swarm.Server.Db.Repositories;
using DiscordBot.Commands;

namespace Swarm.Server.Extensions;

public static class SwarmApplicationExtensions
{
    // Inicializacija baze ob zagonu.
    public static async Task InitializeSwarmAsync(this WebApplication app)
    {
        var database = app.Services.GetRequiredService<CreateDatabase>();
        await database.InitializeAsync();

        var sessions = app.Services.GetRequiredService<SessionRepository>();
        await sessions.DisconnectAllSessionsAsync();

        // Zažene diagnostiko.
        _ = app.Services.GetRequiredService<Swarm.Server.Diagnostics.ConnectionDiagnostics>();
    }

    // Registracija Discord ukazov.
    public static async Task RegisterSwarmCommandsAsync(this WebApplication app)
    {
        app.AddModules(typeof(PingCommand).Assembly);

        var gateway = app.Services.GetRequiredService<NetCord.Gateway.GatewayClient>();

        var commands = app.Services.GetRequiredService<
            NetCord.Services.ApplicationCommands.ApplicationCommandService<
                NetCord.Services.ApplicationCommands.ApplicationCommandContext>>();

        await commands.RegisterCommandsAsync(gateway.Rest, gateway.Id);
    }

    // Nastavitev HTTP strežnika.
    public static void ConfigureSwarmPipeline(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
            app.MapOpenApi();

        app.UseWebSockets(new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromMinutes(2)
        });

        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.UseRateLimiter();

        app.MapControllers();
    }
}
