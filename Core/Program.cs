// See https://aka.ms/new-console-template for more information

using Core;
using Core.ArgParse;
using Core.SiteParsing;

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
        Console.WriteLine(output);
        break;
    case RunMode.Gui:
        //await Gui();
        Console.WriteLine("Run the GUI through the GUI project");
        break;
    case RunMode.Cli:
        var ripper = new NicheImageRipperCli();
        await ripper.Run();
        break;
    default:
        throw new ArgumentOutOfRangeException();
}