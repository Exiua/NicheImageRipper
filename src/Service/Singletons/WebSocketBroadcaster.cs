using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;
using NicheImageRipper.Service.Configuration;
using NicheImageRipper.Service.Models.WebSocket;
using Serilog.Events;

namespace NicheImageRipper.Service.Singletons;

/// <summary>
///     Sends events to every client connected to <c>/ws/events</c>. Each client has its own outbox drained by a single
///     send loop, because a <see cref="WebSocket"/> allows only one send at a time, and its own log level.
/// </summary>
public sealed class WebSocketBroadcaster
{
    /// <summary>Messages queued per client; a client this far behind loses its oldest ones.</summary>
    internal const int OutboxCapacity = 1000;

    /// <summary>Largest message accepted from a client. Client messages are tiny; anything bigger is ignored.</summary>
    private const int MaxClientMessageBytes = 4096;

    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<Guid, Client> _clients = new();
    private readonly ILoggerFactory _loggerFactory;
    private readonly ServiceLoggingOptions _loggingOptions;

    // Created lazily: the Serilog sink resolves this class while logging, so the constructor must not log
    private ILogger<WebSocketBroadcaster> Logger => field ??= _loggerFactory.CreateLogger<WebSocketBroadcaster>();

    public WebSocketBroadcaster(ILoggerFactory loggerFactory, ServiceLoggingOptions loggingOptions)
    {
        _loggerFactory = loggerFactory;
        _loggingOptions = loggingOptions;
    }

    public int ClientCount => _clients.Count;

    /// <summary>Whether any connected client wants log events of this level.</summary>
    public bool WantsLog(LogEventLevel level)
    {
        foreach (var client in _clients.Values)
        {
            if (level >= client.LogLevel)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Queues an event for every client.</summary>
    public void Broadcast(WsEnvelope envelope)
    {
        if (_clients.IsEmpty)
        {
            return;
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope);
        foreach (var client in _clients.Values)
        {
            client.Enqueue(bytes);
        }
    }

    /// <summary>Queues a log event for the clients subscribed to its level.</summary>
    public void BroadcastLog(LogEventLevel level, WsEnvelope envelope)
    {
        byte[]? bytes = null;
        foreach (var client in _clients.Values)
        {
            if (level < client.LogLevel)
            {
                continue;
            }

            bytes ??= JsonSerializer.SerializeToUtf8Bytes(envelope);
            client.Enqueue(bytes);
        }
    }

    /// <summary>The level a client gets when it asks for <paramref name="requested"/> (or nothing).</summary>
    public LogEventLevel ResolveLogLevel(LogEventLevel? requested)
    {
        var level = requested ?? _loggingOptions.WebSocketDefaultLevel;
        return level < _loggingOptions.WebSocketMinimumLevel ? _loggingOptions.WebSocketMinimumLevel : level;
    }

    /// <summary>Serves one client until it disconnects or <paramref name="cancellationToken"/> fires.</summary>
    public async Task AddClientAndWaitAsync(WebSocket socket, LogEventLevel? requestedLogLevel,
                                            CancellationToken cancellationToken)
    {
        var client = new Client(socket, ResolveLogLevel(requestedLogLevel));
        using var sendCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        client.Enqueue(LogLevelChangedMessage(client.LogLevel));
        var sendLoop = SendLoopAsync(client, sendCancellation.Token);
        _clients[client.Id] = client;
        Logger.LogInformation("WebSocket client {ClientId} connected (log level {LogLevel})", client.Id,
            client.LogLevel);

        try
        {
            await ReceiveLoopAsync(client, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested ||
                                                  socket.State == WebSocketState.Aborted)
        {
            // Request aborted, the service is stopping, or the send loop dropped the client
        }
        catch (WebSocketException e)
        {
            Logger.LogDebug(e, "WebSocket client {ClientId} disconnected without closing", client.Id);
        }
        finally
        {
            _clients.TryRemove(client.Id, out _);
            client.Outbox.Writer.TryComplete();
            await sendCancellation.CancelAsync();
            await sendLoop;
            await CloseAsync(client);
            socket.Dispose();
            Logger.LogInformation("WebSocket client {ClientId} disconnected", client.Id);
        }
    }

    private async Task SendLoopAsync(Client client, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in client.Outbox.Reader.ReadAllAsync(cancellationToken))
            {
                await client.Socket.SendAsync(message, WebSocketMessageType.Text, endOfMessage: true,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception e) when (e is WebSocketException or ObjectDisposedException)
        {
            Logger.LogDebug(e, "Sending to WebSocket client {ClientId} failed; dropping it", client.Id);
            client.Socket.Abort(); // ends the receive loop, which removes the client
        }
    }

    private async Task ReceiveLoopAsync(Client client, CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxClientMessageBytes];
        while (client.Socket.State == WebSocketState.Open)
        {
            var length = 0;
            var tooBig = false;
            ValueWebSocketReceiveResult result;
            do
            {
                if (length == buffer.Length)
                {
                    // Keep reading to the end of the message, but throw it away
                    tooBig = true;
                    length = 0;
                }

                result = await client.Socket.ReceiveAsync(buffer.AsMemory(length), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                length += result.Count;
            }
            while (!result.EndOfMessage);

            if (tooBig)
            {
                Logger.LogWarning("Ignored a message over {MaxBytes} bytes from WebSocket client {ClientId}",
                    MaxClientMessageBytes, client.Id);
                continue;
            }

            if (result.MessageType == WebSocketMessageType.Text)
            {
                HandleClientMessage(client, buffer.AsSpan(0, length));
            }
        }
    }

    private void HandleClientMessage(Client client, ReadOnlySpan<byte> json)
    {
        WsClientMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<WsClientMessage>(json);
        }
        catch (JsonException e)
        {
            Logger.LogWarning("Ignored a malformed message from WebSocket client {ClientId}: {Error}", client.Id,
                e.Message);
            return;
        }

        switch (message?.Type)
        {
            case WsClientMessageType.SetLogLevel when message.LogLevel is { } requested:
                client.LogLevel = ResolveLogLevel((LogEventLevel)requested);
                client.Enqueue(LogLevelChangedMessage(client.LogLevel));
                Logger.LogDebug("WebSocket client {ClientId} set its log level to {LogLevel}", client.Id,
                    client.LogLevel);
                break;
            default:
                Logger.LogWarning("Ignored an unsupported message from WebSocket client {ClientId}", client.Id);
                break;
        }
    }

    private static byte[] LogLevelChangedMessage(LogEventLevel level)
    {
        return JsonSerializer.SerializeToUtf8Bytes(new WsEnvelope
        {
            EventType = WsEventType.LogLevelChanged,
            Payload = JsonSerializer.SerializeToElement(new LogLevelChangedEvent { LogLevel = (WsLogLevel)level }),
        });
    }

    private async Task CloseAsync(Client client)
    {
        if (client.Socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
        {
            return;
        }

        try
        {
            using var timeout = new CancellationTokenSource(CloseTimeout);
            await client.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", timeout.Token);
        }
        catch (Exception e) when (e is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            Logger.LogDebug(e, "Closing WebSocket client {ClientId} failed", client.Id);
        }
    }

    private sealed class Client(WebSocket socket, LogEventLevel logLevel)
    {
        private volatile LogEventLevel _logLevel = logLevel;

        public Guid Id { get; } = Guid.NewGuid();
        public WebSocket Socket { get; } = socket;

        public Channel<byte[]> Outbox { get; } = Channel.CreateBounded<byte[]>(
            new BoundedChannelOptions(OutboxCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
            });

        public LogEventLevel LogLevel
        {
            get => _logLevel;
            set => _logLevel = value;
        }

        public void Enqueue(byte[] message) => Outbox.Writer.TryWrite(message);
    }
}
