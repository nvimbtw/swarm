using DotNetEnv;
using Swarm.Server.Extensions;
using Swarm.Server.Db.Repositories;

// Naloži .env konfiguracijo.
Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Nastavi Discord token.
builder.Configuration["Discord:Token"] = Env.GetString("DISCORD_TOKEN");

// Registrira vse storitve aplikacije.
builder.Services.AddSwarmServices();

var app = builder.Build();

// Pripravi bazo in označi stare seje kot odklopljene.
await app.InitializeSwarmAsync();

// Zažene Discord ukaze in HTTP strežnik.
await app.RegisterSwarmCommandsAsync();
app.ConfigureSwarmPipeline();

await app.RunAsync();
