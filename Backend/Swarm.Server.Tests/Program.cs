using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Swarm.Server.Connections;
using Swarm.Server.Controllers;
using Swarm.Server.Diagnostics;
using DiscordBot.Commands;
using NetCord.Services.ApplicationCommands;

if (args.Contains("--serve"))
{
    // A separate loopback-only test host: never included in the production application.
    var builder = WebApplication.CreateBuilder(args.Where(arg => arg != "--serve").ToArray());
    builder.Logging.ClearProviders();
    builder.WebHost.UseUrls(builder.Configuration["test-url"] ?? "http://127.0.0.1:0");
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddSingleton<PairingService>();
    builder.Services.AddSingleton<SessionRegistry>();
    builder.Services.AddSingleton<ConnectionDiagnostics>();
    builder.Services.AddControllers().AddApplicationPart(typeof(PairingController).Assembly);
    builder.Services.AddRateLimiter(o => o.AddFixedWindowLimiter("pairing", limiter =>
    {
        limiter.PermitLimit = 1000;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    }));
    var app = builder.Build();
    app.UseWebSockets(new() { KeepAliveInterval = TimeSpan.FromSeconds(5) });
    app.UseRateLimiter();
    var sockets = new System.Collections.Concurrent.ConcurrentDictionary<string, HttpContext>();
    app.Use(async (context, next) =>
    {
        if (context.Request.Path != "/ws/sessions") { await next(context); return; }
        sockets[context.TraceIdentifier] = context;
        try { await next(context); }
        finally { sockets.TryRemove(context.TraceIdentifier, out _); }
    });
    app.MapControllers();
    app.MapPost("/test/approve/{id}", (string id, PairingService pairing) =>
        Results.Json(pairing.Approve(id, "discord-test-owner")));
    app.MapPost("/test/drop-connections", () =>
    {
        foreach (var context in sockets.Values) context.Abort();
        return Results.Ok();
    });
    await app.StartAsync();
    Console.WriteLine(app.Urls.Single());
    await app.WaitForShutdownAsync();
    return;
}

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

var commands = new ApplicationCommandService<ApplicationCommandContext>();
commands.AddModules(typeof(PingCommand).Assembly);
var commandNames = commands.GetCommands().Select(command => command.Name).ToArray();
Console.WriteLine("Discovered Discord commands: " + string.Join(", ", commandNames));
Check(commandNames.Contains("ping"), "Ping command must be discoverable");
Check(commandNames.Contains("swarm-pair"), "Pairing command must be discoverable");

var clock = new TestClock();
var service = new PairingService(clock);
var first = service.Start("machine", "Laptop");
Check(service.Complete(first.PairingId, first.Secret, "00000000") is null, "Must approve through Discord");
var code = service.Approve(first.PairingId, "owner")!;
Check(service.Approve(first.PairingId, "attacker") is null, "Cannot overwrite the verified Discord owner");
Check(service.Complete(first.PairingId, "wrong-secret", code.Code) is null, "Code alone cannot claim pairing");
var issued = service.Complete(first.PairingId, first.Secret, code.Code)!;
var identity = service.Authenticate(issued.Token)!;
Check(identity.OwnerId == "owner", "Owner comes from Discord");
Check(service.Complete(first.PairingId, first.Secret, code.Code) is null, "Code is single use");
var exhausted = service.Start("other", "Other");
var exhaustedCode = service.Approve(exhausted.PairingId, "owner")!;
for (int attempt = 0; attempt < 5; attempt++)
    Check(service.Complete(exhausted.PairingId, exhausted.Secret, "wrong") is null, "Wrong code rejected");
Check(service.Complete(exhausted.PairingId, exhausted.Secret, exhaustedCode.Code) is null, "Five attempts exhaust pairing");
var expired = service.Start("expired", "Expired");
var expiredCode = service.Approve(expired.PairingId, "owner")!;
clock.Now = clock.Now.AddMinutes(6);
Check(service.Complete(expired.PairingId, expired.Secret, expiredCode.Code) is null, "Expired pairing rejected");
var replacement = service.Start("machine", "Laptop");
var replacementCode = service.Approve(replacement.PairingId, "owner")!;
Check(service.Complete(replacement.PairingId, replacement.Secret, replacementCode.Code) is not null, "Re-pair succeeds");
Check(service.Authenticate(issued.Token) is null, "Re-pair replaces old installation credential");

var registry = new SessionRegistry();
var connectionA = Guid.NewGuid();
var connectionB = Guid.NewGuid();
var registration = new SessionRegistration(Guid.NewGuid(), "A", "project", "codex", "solo");
registry.Register(connectionA, identity, registration);
registry.Register(connectionB, identity, registration with { SessionId = Guid.NewGuid(), Name = "B" });
Check(registry.List("stranger").Length == 0, "Session list is owner scoped");
Check(!registry.Remove(connectionB, registration.SessionId), "Another connection cannot unregister this session");
bool collisionRejected = false;
try { registry.Register(connectionB, identity with { MachineId = "another-machine" }, registration); }
catch (InvalidOperationException) { collisionRejected = true; }
Check(collisionRejected, "Another installation cannot take over a session ID");
registry.Disconnect(connectionA);
Check(registry.List("owner").Length == 1, "Disconnect affects only its own sessions");
registry.Register(connectionA, identity, registration);
registry.Register(connectionB, identity, registration);
registry.Disconnect(connectionA);
Check(registry.List("owner").Length == 2, "Old transport cleanup cannot remove a reconnected session");
Console.WriteLine("Pairing and session ownership checks passed.");

sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => Now;
}
