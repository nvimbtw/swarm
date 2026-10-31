using DotNetEnv;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;

using DiscordBot.Commands;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Discord konfiguracija
builder.Configuration["Discord:Token"] =
    Env.GetString("DISCORD_TOKEN");

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Discord Gateway
builder.Services
    .AddDiscordGateway()
    .AddApplicationCommands();

var app = builder.Build();

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

app.UseWebSockets(webSocketOptions);

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

await app.RunAsync();