using System.Text;
using NicheImageRipper.Core.Driver;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Tui;
using Serilog;
using Serilog.Events;

#if DEBUG
using System.CommandLine;
#endif

Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Console(levelSwitch: NicheImageRipper.Core.NicheImageRipper.ConsoleLoggingLevelSwitch)
            .WriteTo.File("Logs/app.log", rollingInterval: RollingInterval.Day, restrictedToMinimumLevel: LogEventLevel.Debug)
            .CreateLogger();

#if DEBUG
NicheImageRipper.Core.NicheImageRipper.ConsoleLoggingLevelSwitch.MinimumLevel = LogEventLevel.Debug;
#endif

Console.OutputEncoding = Encoding.UTF8;

#if DEBUG
// "test"/"gui" are debug-only parser-debugging entry points, orthogonal to the rip CLI/REPL
if (args.Length > 0 && args[0].Equals("test", StringComparison.OrdinalIgnoreCase))
{
    await RunTestMode(args[1..]);
}
else if (args.Length > 0 && args[0].Equals("gui", StringComparison.OrdinalIgnoreCase))
{
    Log.Error("Run the GUI through the GUI project");
}
else
{
    await new NicheImageRipperCli().Run(args);
}
#else
await new NicheImageRipperCli().Run(args);
#endif

#if DEBUG
async Task RunTestMode(string[] testArgs)
{
    var urlArg = new Argument<string>("url");
    var debugOption = new Option<bool>("--debug", "-d") { Description = "Run non-headless and pause after parsing" };
    var printSiteOption = new Option<bool>("--print-site", "-p") { Description = "Dump page source to test.html" };

    var testCommand = new Command("test", "Debug-run a single parser against one URL")
    {
        urlArg, debugOption, printSiteOption
    };

    testCommand.SetAction(async (parseResult, ct) =>
    {
        var url = parseResult.GetValue(urlArg)!;
        var debug = parseResult.GetValue(debugOption);
        var printSite = parseResult.GetValue(printSiteOption);

        var requestHeaders = new Dictionary<string, string>
        {
            { "User-Agent", Config.Instance.UserAgent },
            { "referer", "https://imhentai.xxx/" },
            { "cookie", "" }
        };

        using var pool = new WebDriverPool(1);
        var driver = pool.AcquireDriver(!debug); // if debug, headless = false
        var parser = new HtmlParserOrchestrator(driver, requestHeaders);
        var output = await parser.TestParse(url, debug, printSite, ct);
        pool.ReleaseDriver(driver);
        Log.Information("{RipInfo}", output);
    });

    var root = new RootCommand { testCommand };
    await root.Parse(["test", ..testArgs]).InvokeAsync();
}
#endif