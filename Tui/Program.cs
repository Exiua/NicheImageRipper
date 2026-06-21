using System.Text;
using NicheImageRipper.Core;
using NicheImageRipper.Core.History;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Core.Utility;
using NicheImageRipper.Core.ArgParse;
using NicheImageRipper.Core.Configuration;
using NicheImageRipper.Core.Driver;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Core.Utility;
using NicheImageRipper.Tui;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Console(levelSwitch: NicheImageRipper.Core.NicheImageRipper.ConsoleLoggingLevelSwitch)
            .WriteTo.File("Logs/app.log", rollingInterval: RollingInterval.Day, restrictedToMinimumLevel: LogEventLevel.Debug)
            .CreateLogger();

#if DEBUG
NicheImageRipper.Core.NicheImageRipper.ConsoleLoggingLevelSwitch.MinimumLevel = LogEventLevel.Debug;
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
            {"User-Agent", Config.Instance.UserAgent},
            {"referer", "https://imhentai.xxx/"},
            {"cookie", ""}
        };

        using var pool = new WebDriverPool(1);
        var driver = pool.AcquireDriver(!arguments.Debug); // if debug, headless = false
        var clientManager = new ApiClientManager();
        var parser = HtmlParser.GetParser("imhentai", driver, clientManager, requestHeaders);
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