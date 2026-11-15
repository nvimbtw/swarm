using Swarm.Server.Classi;
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
            var content = AgentMessagePresentation.ResultText(update);
            try
            {
                var agent = destination.Agent ?? throw new InvalidOperationException("Missing server attribution");
                var context = await AgentMessagePresentation.ResolveAsync(rest, agent, destination.ChannelId, stoppingToken);
                var parts = AgentMessagePresentation.Split(content);
                for (int index = 0; index < parts.Count; index++)
                {
                    var properties = AgentMessagePresentation.Create(
                        parts[index], agent, destination.ChannelId, context, update.RequestId, update.Status,
                        index + 1, parts.Count, destination.Prompt);
                    if (destination.PromptMessageId is { } receiptId)
                        properties.MessageReference = MessageReferenceProperties.Reply(receiptId, failIfNotExists: false);
                    await rest.SendMessageAsync(destination.ChannelId, properties, cancellationToken: stoppingToken);
                }
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                logger.LogWarning("Could not publish agent result {RequestId} ({ErrorType}).",
                    update.RequestId, error.GetType().Name);
            }
        }
    }
}
