using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace CoreService.Singletons;

public sealed class WebSocketLogBroadcaster(ILogger<WebSocketLogBroadcaster> logger)
{
    private readonly ConcurrentDictionary<Guid, WebSocket> _clients = new();

    internal readonly ILogger<WebSocketLogBroadcaster> Logger = logger;

    public async Task AddClientAndWaitAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        _clients[id] = socket;

        var buffer = new byte[1024];

        try
        {
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        catch(Exception e)
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
                    await socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closing",
                        CancellationToken.None);
                }
            }
            catch(Exception e)
            {
                Logger.LogError(e, "Error in WebSocket client connection");
            }

            socket.Dispose();
        }
    }

    public async Task BroadcastAsync(string message, CancellationToken cancellationToken = default)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        var deadClients = new List<Guid>();

        foreach (var (key, socket) in _clients)
        {
            if (socket.State != WebSocketState.Open)
            {
                deadClients.Add(key);
                continue;
            }

            try
            {
                await socket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
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
            catch(Exception e)
            {
                Logger.LogError(e, "Error broadcasting log event");
            }
        }
    }
}