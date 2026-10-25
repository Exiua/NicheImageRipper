using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace NicheImageRipper.Tui;

/// <summary>Writes user-facing CLI output to the console, and records it in the log file too.</summary>
internal static class TuiOutput
{
    /// <summary>Level switch for the console log sink (see Program.cs).</summary>
    public static LoggingLevelSwitch ConsoleLevelSwitch { get; } = new();

    /// <summary>
    ///     Prints <paramref name="message"/> as-is and logs it at <paramref name="level"/>. The console log sink is
    ///     muted for the duration so the message isn't printed a second time as a formatted log line.
    /// </summary>
    public static void Write(string message, LogEventLevel level = LogEventLevel.Information, bool newLine = true)
    {
        var previousLevel = ConsoleLevelSwitch.MinimumLevel;
        ConsoleLevelSwitch.MinimumLevel = LogEventLevel.Fatal + 1;
        try
        {
            if (newLine)
            {
                Console.WriteLine(message);
            }
            else
            {
                Console.Write(message);
            }

            Log.Write(level, message);
        }
        finally
        {
            ConsoleLevelSwitch.MinimumLevel = previousLevel;
        }
    }
}
