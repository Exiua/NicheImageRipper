using Serilog;

namespace NicheImageRipper.Sdk.Interaction;

/// <summary>Lets parsers and download strategies ask the person running the ripper to do something by hand.</summary>
public interface IUserPrompt
{
    /// <summary>
    ///     Shows <paramref name="message"/> (e.g. "solve the captcha in the browser window") and completes once the
    ///     user says they're done.
    /// </summary>
    Task WaitForUserAsync(string message, CancellationToken cancellationToken = default);
}

public static class UserPrompt
{
    /// <summary>
    ///     Set by the host at startup: a dialog in the GUI, a console prompt in the Tui. Defaults to logging the
    ///     request and carrying on, for hosts with nobody to ask (e.g. the Service).
    /// </summary>
    public static IUserPrompt Current { get; set; } = new LoggingUserPrompt();

    private sealed class LoggingUserPrompt : IUserPrompt
    {
        private static readonly ILogger Logger = Log.ForContext<LoggingUserPrompt>();

        public Task WaitForUserAsync(string message, CancellationToken cancellationToken = default)
        {
            Logger.Warning("Manual action needed, but this host can't prompt anyone; continuing without it: {Message}",
                message);
            return Task.CompletedTask;
        }
    }
}
