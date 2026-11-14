
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services.ApplicationCommands;
using Swarm.Server.Classi;
using Swarm.Server.Connections;
using Swarm.Server.Db;
using Swarm.Server.Db.Repositories;
using Swarm.Server.Diagnostics;
using DiscordBot.Commands;

namespace Swarm.Server.Extensions;

public static class SwarmServiceExtensions
{
    public static IServiceCollection AddSwarmServices(this IServiceCollection services)
    {
        // HTTP storitve.
        services.AddControllers();
        services.AddOpenApi();
        services.AddAuthorization();

        // Baza in repositoryji.
        services.AddSingleton<DatabaseService>();
        services.AddSingleton<CreateDatabase>();
        services.AddSingleton<CredentialRepository>();
        services.AddSingleton<SessionRepository>();
        services.AddSingleton<WorkRequestRepository>();

        // Povezave in agenti.
        services.AddSingleton<ConnectionDiagnostics>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<PairingService>();
        services.AddSingleton<SessionRegistry>();
        services.AddSingleton<AgentRouter>();

        // Discord publisher.
        services.AddSingleton<AgentDiscordPublisher>();
        services.AddSingleton<IAgentEventSink>(s =>
            s.GetRequiredService<AgentDiscordPublisher>());
        services.AddHostedService(s =>
            s.GetRequiredService<AgentDiscordPublisher>());

        // Discord Gateway in ukazi.
        services
            .AddDiscordGateway()
            .AddApplicationCommands<
                NetCord.ApplicationCommandInteraction,
                NetCord.Services.ApplicationCommands.ApplicationCommandContext,
                NetCord.Services.ApplicationCommands.AutocompleteInteractionContext>();

        // Omejitev zahtev za pairing.
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy("pairing", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
        });

        return services;
    }
}
