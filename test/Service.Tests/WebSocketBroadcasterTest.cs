using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NicheImageRipper.Service.Configuration;
using NicheImageRipper.Service.Models.WebSocket;
using NicheImageRipper.Service.Serilog;
using NicheImageRipper.Service.Singletons;
using Serilog.Events;
using Serilog.Parsing;

namespace NicheImageRipper.Service.Tests;

public sealed class WebSocketBroadcasterTest : IAsyncDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly WebSocketBroadcaster _broadcaster = new(NullLoggerFactory.Instance, new ServiceLoggingOptions
    {
        WebSocketMinimumLevel = LogEventLevel.Debug,
        WebSocketDefaultLevel = LogEventLevel.Information,
    });

    private readonly List<(FakeWebSocket Socket, Task Connection)> _clients = [];

    public async ValueTask DisposeAsync()
    {
        foreach (var (socket, connection) in _clients)
        {
            socket.ClientCloses();
            await connection.WaitAsync(Timeout);
        }
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<FakeWebSocket> ConnectAsync(LogEventLevel? logLevel = null)
    {
        var socket = new FakeWebSocket();
        _clients.Add((socket, _broadcaster.AddClientAndWaitAsync(socket, logLevel, Ct)));
        // The first message is always the applied log level
        await WaitForAsync(() => socket.Sent.Count >= 1);
        return socket;
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition not met in time");
            }

            await Task.Delay(5, Ct);
        }
    }

    private static List<WsEnvelope> Received(FakeWebSocket socket) =>
        socket.Sent.Select(json => JsonSerializer.Deserialize<WsEnvelope>(json)!).ToList();

    private static WsLogLevel AppliedLevel(WsEnvelope envelope)
    {
        Assert.Equal(WsEventType.LogLevelChanged, envelope.EventType);
        return envelope.Payload.Deserialize<LogLevelChangedEvent>()!.LogLevel;
    }

    private static WsEnvelope LogEnvelope(string message) => new()
    {
        EventType = WsEventType.Log,
        Payload = JsonSerializer.SerializeToElement(new { message }),
    };

    [Fact]
    public async Task OverlappingBroadcastsAreSentOneAtATime()
    {
        var socket = await ConnectAsync();
        const int count = 300;

        // Ripper events and log events arrive on many threads at once
        Parallel.For(0, count, _ => _broadcaster.Broadcast(new WsEnvelope { EventType = WsEventType.QueueUpdate }));

        await WaitForAsync(() => socket.Sent.Count == count + 1);
        Assert.False(socket.OverlappingSendDetected);
        Assert.Equal(1, _broadcaster.ClientCount);
    }

    [Fact]
    public async Task ClientWithoutALevelGetsTheDefault()
    {
        var socket = await ConnectAsync();

        Assert.Equal(WsLogLevel.Information, AppliedLevel(Received(socket)[0]));
    }

    [Fact]
    public async Task RequestBelowTheMinimumIsRaisedToIt()
    {
        var socket = await ConnectAsync(LogEventLevel.Verbose);

        Assert.Equal(WsLogLevel.Debug, AppliedLevel(Received(socket)[0]));
    }

    [Fact]
    public async Task LogsBelowTheClientsLevelAreNotSent()
    {
        var socket = await ConnectAsync(LogEventLevel.Warning);

        _broadcaster.BroadcastLog(LogEventLevel.Information, LogEnvelope("info"));
        _broadcaster.BroadcastLog(LogEventLevel.Error, LogEnvelope("error"));

        // Both were queued in order, so if "info" had been sent it would be here before "error"
        await WaitForAsync(() => socket.Sent.Count == 2);
        Assert.Contains("error", socket.Sent.Last());
        Assert.False(_broadcaster.WantsLog(LogEventLevel.Information));
        Assert.True(_broadcaster.WantsLog(LogEventLevel.Warning));
    }

    [Fact]
    public async Task EachClientGetsItsOwnLevel()
    {
        var detailed = await ConnectAsync(LogEventLevel.Debug);
        var quiet = await ConnectAsync(LogEventLevel.Error);

        _broadcaster.BroadcastLog(LogEventLevel.Debug, LogEnvelope("debug"));
        _broadcaster.BroadcastLog(LogEventLevel.Error, LogEnvelope("error"));

        await WaitForAsync(() => detailed.Sent.Count == 3 && quiet.Sent.Count == 2);
        Assert.Contains("debug", detailed.Sent.ElementAt(1));
        Assert.Contains("error", quiet.Sent.Last());
    }

    [Fact]
    public async Task ClientCanChangeItsLevel()
    {
        var socket = await ConnectAsync(LogEventLevel.Warning);

        socket.ClientSends("""{"Type":"SetLogLevel","LogLevel":"Verbose"}""");

        await WaitForAsync(() => socket.Sent.Count == 2);
        Assert.Equal(WsLogLevel.Debug, AppliedLevel(Received(socket)[1])); // raised to the service's minimum
        _broadcaster.BroadcastLog(LogEventLevel.Debug, LogEnvelope("debug"));
        await WaitForAsync(() => socket.Sent.Count == 3);
    }

    [Fact]
    public async Task MalformedClientMessagesAreIgnored()
    {
        var socket = await ConnectAsync();

        socket.ClientSends("not json");
        socket.ClientSends("""{"Type":"SomethingElse"}""");
        socket.ClientSends("""{"Type":"SetLogLevel","LogLevel":"Error"}""");

        await WaitForAsync(() => socket.Sent.Count == 2);
        Assert.Equal(WsLogLevel.Error, AppliedLevel(Received(socket)[1]));
        Assert.Equal(1, _broadcaster.ClientCount);
    }

    [Fact]
    public async Task ClosingRemovesTheClient()
    {
        var socket = await ConnectAsync();
        var (_, connection) = _clients.Single();
        _clients.Clear();

        socket.ClientCloses();
        await connection.WaitAsync(Timeout, Ct);

        Assert.Equal(0, _broadcaster.ClientCount);
    }

    [Fact]
    public async Task SinkSkipsTheBroadcastersOwnEvents()
    {
        var socket = await ConnectAsync(LogEventLevel.Debug);
        var sink = new WebSocketLogSink(() => _broadcaster);

        sink.Emit(CreateLogEvent("from the broadcaster", typeof(WebSocketBroadcaster).FullName!));
        sink.Emit(CreateLogEvent("from the ripper", "NicheImageRipper.Core.NicheImageRipper"));

        await WaitForAsync(() => socket.Sent.Count == 2);
        Assert.Contains("from the ripper", socket.Sent.Last());
    }

    private static LogEvent CreateLogEvent(string message, string sourceContext)
    {
        return new LogEvent(DateTimeOffset.Now, LogEventLevel.Information, null,
            new MessageTemplateParser().Parse(message),
            [new LogEventProperty("SourceContext", new ScalarValue(sourceContext))]);
    }
}
