using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using NicheImageRipper.Service.Models.WebSocket;

namespace NicheImageRipper.Service.Singletons;

public sealed class WebSocketBroadcaster
{
    private readonly ConcurrentDictionary<Guid, WebSocket> _clients = new();
    private readonly INicheImageRipperSingleton _nicheImageRipperSingleton;
    private readonly ILoggerFactory _loggerFactory;
    internal ILogger<WebSocketBroadcaster> Logger => field ??= _loggerFactory.CreateLogger<WebSocketBroadcaster>();

    public WebSocketBroadcaster(ILoggerFactory loggerFactory, INicheImageRipperSingleton nicheImageRipperSingleton)
    {
        _loggerFactory = loggerFactory;
        _nicheImageRipperSingleton = nicheImageRipperSingleton;
        _nicheImageRipperSingleton.ProgressChanged += OnProgressChanged;
        _nicheImageRipperSingleton.QueueUpdated += OnQueueUpdated;
    }

    private void OnProgressChanged(int current, int total)
    {
        var envelope = new WsEnvelope
        {
            EventType = WsEventType.ProgressChange,
            Payload = JsonSerializer.SerializeToElement(new ProgressChangedEvent { Current = current, Total = total, }),
        };
        _ = BroadcastSafeAsync(envelope);
    }

    private void OnQueueUpdated()
    {
        var envelope = new WsEnvelope
        {
            EventType = WsEventType.QueueUpdate,
        };
        _ = BroadcastSafeAsync(envelope);
    }

    public async Task AddClientAndWaitAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        _clients[id] = socket;
        var buffer = new byte[1024];
        Logger.LogInformation("Adding WebSocket client connection: {Id}", id);
        try
        {
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Error in WebSocket client connection");
        }
        finally
        {
            _clients.TryRemove(id, out _);
            try
            {
                if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error in WebSocket client connection");
            }

            socket.Dispose();
        }
    }

    private async Task BroadcastSafeAsync(WsEnvelope envelope)
    {
        try
        {
            await BroadcastAsync(envelope);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to broadcast WebSocket event {EventType}", envelope.EventType);
        }
    }

    public async Task BroadcastAsync(WsEnvelope message, CancellationToken cancellationToken = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message);
        var deadClients = new List<Guid>();
        foreach (var (key, socket)in _clients)
        {
            if (socket.State != WebSocketState.Open)
            {
                deadClients.Add(key);
                continue;
            }

            try
            {
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, endOfMessage: true,
                    cancellationToken);
            }
            catch
            {
                deadClients.Add(key);
            }
        }

        foreach (var id in deadClients)
        {
            if (!_clients.TryRemove(id, out var socket))
            {
                continue;
            }

            try
            {
                socket.Dispose();
            }
            catch (Exception)
            {
                //Logger.LogError(e, "Error broadcasting log event");
            }
        }
    }
}