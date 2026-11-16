using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.ReactiveUI;
using NicheImageRipper.Gui.Models;
using NicheImageRipper.Gui.Utility;
using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Sdk.Utility;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace NicheImageRipper.Gui;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Important to call this as early as possible
        Config.ReloadConfig<GuiConfig>();

        #if DEBUG
        var consoleSwitch = new LoggingLevelSwitch(LogEventLevel.Debug);
        var fileSwitch = new LoggingLevelSwitch(LogEventLevel.Debug);
        #else
        var consoleSwitch = new LoggingLevelSwitch(LogEventLevel.Information);
        var fileSwitch = new LoggingLevelSwitch(LogEventLevel.Information);
        #endif

        Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .Enrich.FromLogContext()
                    // Writing to the console (and flushing the file) per event blocks the logging thread, which
                    // slows down tight loops that log at Debug. Both are written on a background thread instead;
                    // the console may drop events under a burst, but the file waits so the log stays complete.
                    .WriteTo.Async(sink => sink.Console(theme: AnsiConsoleTheme.Code, levelSwitch: consoleSwitch,
                         outputTemplate:
                         "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext:lj}] {Message:lj}{NewLine}{Exception}"))
                    .WriteTo.Async(sink => sink.File(AppData.GetPath("Logs", "gui.log"),
                         rollingInterval: RollingInterval.Day, levelSwitch: fileSwitch, buffered: true,
                         flushToDiskInterval: TimeSpan.FromSeconds(1)), blockWhenFull: true)
                    .WriteTo.Gui()
                    .CreateLogger();

        // Handle global exceptions
        AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
        {
            Log.Fatal(eventArgs.ExceptionObject as Exception, "Unhandled domain exception");
            if (eventArgs.IsTerminating)
            {
                Log.CloseAndFlush();
            }
        };

        // Handle unobserved task exceptions
        TaskScheduler.UnobservedTaskException += (sender, eventArgs) =>
        {
            Log.Fatal(eventArgs.Exception, "Unobserved task exception");
            eventArgs.SetObserved(); // Prevents application crashes
        };

        Log.Information("Starting application...");
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            // The sinks are async and the file is buffered, so pending events are lost unless flushed
            Log.CloseAndFlush();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
                     .UsePlatformDetect()
                     .WithInterFont()
                     .LogToSerilog()
                     .UseReactiveUI();
}