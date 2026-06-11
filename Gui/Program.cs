using Avalonia;
using Avalonia.ReactiveUI;
using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Configuration;
using Gui.Utility;
using Gui.Models;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace Gui;

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
                    .WriteTo.Console(theme: AnsiConsoleTheme.Code, levelSwitch: consoleSwitch,
                         outputTemplate:
                         "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext:lj}] {Message:lj}{NewLine}{Exception}")
                    .WriteTo.File("Logs/gui.log", rollingInterval: RollingInterval.Day, levelSwitch: fileSwitch)
                    .WriteTo.Gui()
                    .CreateLogger();

        // Handle global exceptions
        AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
        {
            Log.Fatal(eventArgs.ExceptionObject as Exception, "Unhandled domain exception");
        };

        // Handle unobserved task exceptions
        TaskScheduler.UnobservedTaskException += (sender, eventArgs) =>
        {
            Log.Fatal(eventArgs.Exception, "Unobserved task exception");
            eventArgs.SetObserved(); // Prevents application crashes
        };

        Log.Information("Starting application...");
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
                     .UsePlatformDetect()
                     .WithInterFont()
                     .LogToSerilog()
                     .UseReactiveUI();
}