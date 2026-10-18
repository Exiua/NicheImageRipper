using System.Text;
using NicheImageRipper.Sdk.Interaction;
using NicheImageRipper.Sdk.Utility;
using NicheImageRipper.Tui;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Console(levelSwitch: TuiOutput.ConsoleLevelSwitch)
            .WriteTo.File(AppData.GetPath("Logs", "app.log"), rollingInterval: RollingInterval.Day, restrictedToMinimumLevel: LogEventLevel.Debug)
            .CreateLogger();

#if DEBUG
TuiOutput.ConsoleLevelSwitch.MinimumLevel = LogEventLevel.Debug;
#endif

Console.OutputEncoding = Encoding.UTF8;

UserPrompt.Current = new ConsoleUserPrompt();

// Parser debugging (formerly "test" mode) lives in the SiteModulesParserTest project
await new NicheImageRipperCli().Run(args);
