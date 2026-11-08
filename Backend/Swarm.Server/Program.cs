using DotNetEnv;
using NetCord.Rest;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;

using DiscordBot.Commands;
using Swarm.Server.Diagnostics;
using Swarm.Server.Connections;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

using Swarm.Server.Db;

// Find the nearest .env, including Backend/.env when launched from Swarm.Server.
Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Discord konfiguracija
builder.Configuration["Discord:Token"] =
    Env.GetString("DISCORD_TOKEN");

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// database services
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<CreateDatabase>();

builder.Services.AddSingleton<ConnectionDiagnostics>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<PairingService>();
builder.Services.AddSingleton<SessionRegistry>();
builder.Services.AddSingleton<AgentRouter>();
builder.Services.AddSingleton<AgentDiscordPublisher>();
builder.Services.AddSingleton<IAgentEventSink>(s => s.GetRequiredService<AgentDiscordPublisher>());
builder.Services.AddHostedService(s => s.GetRequiredService<AgentDiscordPublisher>());
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("pairing", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

// Discord Gateway
builder.Services
    .AddDiscordGateway()
    .AddApplicationCommands<NetCord.ApplicationCommandInteraction,
        NetCord.Services.ApplicationCommands.ApplicationCommandContext,
        NetCord.Services.ApplicationCommands.AutocompleteInteractionContext>();

var app = builder.Build();

// ustvarjanje database
var database = app.Services.GetRequiredService<CreateDatabase>();
await database.InitializeAsync();

// Start the diagnostics uptime clock during application startup.
_ = app.Services.GetRequiredService<ConnectionDiagnostics>();

// Registracija Discord command modulov
app.AddModules(typeof(PingCommand).Assembly);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2)
};

var gateway = app.Services.GetRequiredService<NetCord.Gateway.GatewayClient>();

var commands = app.Services.GetRequiredService<
    NetCord.Services.ApplicationCommands.ApplicationCommandService<NetCord.Services.ApplicationCommands.ApplicationCommandContext>>();

await commands.RegisterCommandsAsync(gateway.Rest, gateway.Id);

app.UseWebSockets(webSocketOptions);

app.UseHttpsRedirection();

app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

await app.RunAsync();
