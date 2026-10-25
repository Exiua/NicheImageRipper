using System.CommandLine;
using System.Text;
using NicheImageRipper.Core.Driver;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Sdk.Utility;
using Serilog;
using Serilog.Events;

// Dev tool: runs a single site parser against one URL and reports what it found, without downloading anything.
// Writes the found URLs to test.json, and on failure test.html plus a screenshot, in the app data folder (next to
// the executable, or NIR_DATA_DIR), which is also where it reads config.json from.

Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(AppData.GetPath("Logs", "parser-test.log"), rollingInterval: RollingInterval.Day,
                 restrictedToMinimumLevel: LogEventLevel.Debug)
            .CreateLogger();

Console.OutputEncoding = Encoding.UTF8;

var urlArg = new Argument<string>("url") { Description = "Page or gallery URL to parse" };
var debugOption = new Option<bool>("--debug", "-d") { Description = "Run non-headless and pause after parsing" };
var printSiteOption = new Option<bool>("--print-site", "-p") { Description = "Dump page source to test.html" };

var root = new RootCommand("Debug-run a single site parser against one URL")
{
    urlArg, debugOption, printSiteOption
};

root.SetAction(async (parseResult, ct) =>
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
    try
    {
        var parser = new HtmlParserOrchestrator(driver, requestHeaders);
        var output = await parser.TestParse(url, printSite, ct);
        if (debug)
        {
            // Keeps the (non-headless) browser open for inspection until a key is pressed
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        Log.Information("{RipInfo}", output);
    }
    finally
    {
        pool.ReleaseDriver(driver);
    }
});

try
{
    return await root.Parse(args).InvokeAsync();
}
finally
{
    await Log.CloseAndFlushAsync();
}
