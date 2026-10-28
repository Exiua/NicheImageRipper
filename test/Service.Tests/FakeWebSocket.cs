using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;

namespace NicheImageRipper.Service.Tests;

/// <summary>
///     An in-memory server-side WebSocket. Like the real one, it fails a send started while another is still in
///     flight, and records that it happened.
/// </summary>
internal sealed class FakeWebSocket : WebSocket
{
    private readonly Channel<(byte[] Data, WebSocketMessageType Type)> _incoming =
        Channel.CreateUnbounded<(byte[], WebSocketMessageType)>();

    private int _sending;
    private volatile WebSocketState _state = WebSocketState.Open;

    public ConcurrentQueue<string> Sent { get; } = new();
    public bool OverlappingSendDetected { get; private set; }

    /// <summary>How long each send takes; widens the window for overlapping sends.</summary>
    public TimeSpan SendDelay { get; init; } = TimeSpan.FromMilliseconds(1);

    public override WebSocketCloseStatus? CloseStatus => null;
    public override string? CloseStatusDescription => null;
    public override WebSocketState State => _state;
    public override string? SubProtocol => null;

    /// <summary>Simulates the client sending a text message.</summary>
    public void ClientSends(string text) => _incoming.Writer.TryWrite((Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text));

    /// <summary>Simulates the client starting the closing handshake.</summary>
    public void ClientCloses() => _incoming.Writer.TryWrite(([], WebSocketMessageType.Close));

    public override async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType,
                                         bool endOfMessage, CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _sending, 1) == 1)
        {
            OverlappingSendDetected = true;
            throw new InvalidOperationException("There is already one outstanding 'SendAsync' call for this WebSocket.");
        }

        try
        {
            await Task.Delay(SendDelay, cancellationToken);
            Sent.Enqueue(Encoding.UTF8.GetString(buffer));
        }
        finally
        {
            Volatile.Write(ref _sending, 0);
        }
    }

    public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer,
                                                                    CancellationToken cancellationToken)
    {
        (byte[] Data, WebSocketMessageType Type) message;
        try
        {
            message = await _incoming.Reader.ReadAsync(cancellationToken);
        }
        catch (ChannelClosedException e)
        {
            throw new WebSocketException("Aborted", e);
        }

        if (message.Type == WebSocketMessageType.Close)
        {
            _state = WebSocketState.CloseReceived;
            return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true, WebSocketCloseStatus.NormalClosure,
                "");
        }

        message.Data.CopyTo(buffer.Array!, buffer.Offset);
        return new WebSocketReceiveResult(message.Data.Length, message.Type, true);
    }

    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription,
                                    CancellationToken cancellationToken)
    {
        _state = WebSocketState.Closed;
        return Task.CompletedTask;
    }

    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription,
                                          CancellationToken cancellationToken)
    {
        _state = WebSocketState.CloseSent;
        return Task.CompletedTask;
    }

    public override void Abort()
    {
        _state = WebSocketState.Aborted;
        _incoming.Writer.TryComplete();
    }

    public override void Dispose()
    {
        if (_state != WebSocketState.Closed)
        {
            Abort();
        }
    }
}
