using System.Text.Json;
using NicheImageRipper.Service.Models.WebSocket;
using NicheImageRipper.Service.Singletons;

namespace NicheImageRipper.Service.Services;

/// <summary>
///     Forwards ripper events to WebSocket clients while the service runs, and stops and saves the ripper when it
///     shuts down.
/// </summary>
public sealed class RipperLifetimeService(
    INicheImageRipperSingleton ripper,
    WebSocketBroadcaster broadcaster,
    ILogger<RipperLifetimeService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        ripper.ProgressChanged += OnProgressChanged;
        ripper.QueueUpdated += OnQueueUpdated;
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        ripper.ProgressChanged -= OnProgressChanged;
        ripper.QueueUpdated -= OnQueueUpdated;
        logger.LogInformation("Stopping the ripper and saving its state");
        try
        {
            await ripper.StopAsync(cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to save the ripper's state on shutdown");
        }
    }

    private void OnProgressChanged(int current, int total)
    {
        broadcaster.Broadcast(new WsEnvelope
        {
            EventType = WsEventType.ProgressChange,
            Payload = JsonSerializer.SerializeToElement(new ProgressChangedEvent { Current = current, Total = total }),
        });
    }

    private void OnQueueUpdated()
    {
        broadcaster.Broadcast(new WsEnvelope { EventType = WsEventType.QueueUpdate });
    }
}
