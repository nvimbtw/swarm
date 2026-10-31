
using DotNetEnv;
using Microsoft.Extensions.Hosting;

using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;


using DiscordBot.Commands;

Env.Load();

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration["Discord:Token"] =
    Env.GetString("DISCORD_TOKEN");

builder.Services
    .AddDiscordGateway()
    .AddApplicationCommands();

var host = builder.Build();

host.AddModules(typeof(PingCommand).Assembly);

await host.RunAsync();
