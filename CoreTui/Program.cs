using System.Text;
using Core;
using Core.ArgParse;
using Core.Driver;
using Core.History;
using Core.SiteParsing;
using Core.Utility;
using CoreTui;
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
Console.OutputEncoding = Encoding.UTF8;

#if DEBUG
var arguments = ArgumentParser.Parse(args);
switch (arguments.RunMode)
{
    case RunMode.Test:
    {
        var requestHeaders = new Dictionary<string, string>
        {
            {"User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.190 Safari/537.36"},
            {"referer", "https://imhentai.xxx/"},
            {"cookie", ""}
        };

        using var pool = new WebDriverPool(1);
        var driver = pool.AcquireDriver(!arguments.Debug); // if debug, headless = false
        var parser = HtmlParser.GetParser("imhentai", driver, requestHeaders);
        // Null check performed in ArgumentParser.Parse
        var output = await parser.TestParse(arguments.Url!, arguments.Debug, arguments.PrintSite);
        pool.ReleaseDriver(driver);
        Log.Information("{ripInfo}", output);
        break;
    }
    case RunMode.Gui:
        Log.Error("Run the GUI through the GUI project");
        break;
    case RunMode.Cli:
        var ripper = new NicheImageRipperCli();
        await ripper.Run();
        break;
    default:
        throw new ArgumentOutOfRangeException();
}
#else
var ripper = new NicheImageRipperCli();
await ripper.Run();
#endif