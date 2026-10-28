using Serilog.Events;

namespace NicheImageRipper.Service.Configuration;

/// <summary>The <c>ServiceLogging</c> section of appsettings.</summary>
public sealed class ServiceLoggingOptions
{
    public const string SectionName = "ServiceLogging";

    /// <summary>Lowest level written to the console and the log file.</summary>
    public LogEventLevel MinimumLevel { get; set; } = LogEventLevel.Information;

    /// <summary>
    ///     Lowest level a WebSocket client can subscribe to. Asking for more detail gets this level instead. Events
    ///     below <see cref="MinimumLevel"/> are still produced when this is lower, so keep it at Debug or above unless
    ///     you need Verbose output.
    /// </summary>
    public LogEventLevel WebSocketMinimumLevel { get; set; } = LogEventLevel.Debug;

    /// <summary>Level used for WebSocket clients that don't ask for one.</summary>
    public LogEventLevel WebSocketDefaultLevel { get; set; } = LogEventLevel.Information;

    /// <summary>Lowest level any sink needs, i.e. the level the logger itself must let through.</summary>
    public LogEventLevel PipelineMinimumLevel =>
        (LogEventLevel)Math.Min((int)MinimumLevel, (int)WebSocketMinimumLevel);
}
