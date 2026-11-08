using System.Threading.Channels;
using NetCord.Rest;
using Swarm.Server.Connections;

namespace DiscordBot.Commands;

/// <summary>Keeps Discord REST latency out of the shared socket receive loop.</summary>
public sealed class AgentDiscordPublisher(RestClient rest, ILogger<AgentDiscordPublisher> logger)
    : BackgroundService, IAgentEventSink
{
    private readonly Channel<(AgentDestination Destination, AgentEvent Update)> _updates =
        Channel.CreateBounded<(AgentDestination, AgentEvent)>(new BoundedChannelOptions(256)
        { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    private CancellationToken _stoppingToken;

    public async ValueTask PublishAsync(AgentDestination destination, AgentEvent update)
    {
        if (destination.ChannelId == 0 || update.Status == "started") return;
        try { await _updates.Writer.WriteAsync((destination, update), _stoppingToken); }
        catch (OperationCanceledException) when (_stoppingToken.IsCancellationRequested) { }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;
        await foreach (var (destination, update) in _updates.Reader.ReadAllAsync(stoppingToken))
        {
            var content = $"Agent request `{update.RequestId}` · session `{update.SessionId}` · {update.Status}";
            if (!string.IsNullOrEmpty(update.Error)) content += $"\n{update.Error}";
            if (!string.IsNullOrEmpty(update.Output)) content += $"\n\n{update.Output}";
            try
            {
                for (int offset = 0; offset < content.Length; offset += 1800)
                    await rest.SendMessageAsync(destination.ChannelId, new MessageProperties
                    {
                        Content = content.Substring(offset, Math.Min(1800, content.Length - offset)),
                        AllowedMentions = AllowedMentionsProperties.None
                    }, cancellationToken: stoppingToken);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                logger.LogWarning("Could not publish agent result {RequestId} ({ErrorType}).",
                    update.RequestId, error.GetType().Name);
            }
        }
    }
}
