// See https://aka.ms/new-console-template for more information

using Core;
using Core.ArgParse;
using Core.SiteParsing;
using Core.Utility;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Console(levelSwitch: NicheImageRipper.ConsoleLoggingLevelSwitch)
            .WriteTo.File("Logs/app.log", rollingInterval: RollingInterval.Day, restrictedToMinimumLevel: LogEventLevel.Debug)
            .CreateLogger();

#if DEBUG
NicheImageRipper.ConsoleLoggingLevelSwitch.MinimumLevel = LogEventLevel.Debug;
#endif

PrintUtility.PrintFunction = Log.Information;

var arguments = ArgumentParser.Parse(args);
switch (arguments.RunMode)
{
    case RunMode.Test:
        var requestHeaders = new Dictionary<string, string>
        {
            {"User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.190 Safari/537.36"},
            {"referer", "https://imhentai.xxx/"},
            {"cookie", ""}
        };

        var parser = new HtmlParser(requestHeaders);
        // Null check performed in ArgumentParser.Parse
        var output = await parser.TestParse(arguments.Url!, arguments.Debug, arguments.PrintSite);
        Log.Information("{ripInfo}", output);
        break;
    case RunMode.Gui:
        //await Gui();
        Log.Error("Run the GUI through the GUI project");
        break;
    case RunMode.Cli:
        var ripper = new NicheImageRipperCli();
        await ripper.Run();
        break;
    default:
        throw new ArgumentOutOfRangeException();
}