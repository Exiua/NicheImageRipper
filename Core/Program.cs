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
        break;
    case RunMode.Cli:
        var ripper = new NicheImageRipperCli();
        await ripper.Run();
        break;
    default:
        throw new ArgumentOutOfRangeException();
}



//await GDriveHelper.Test("https://drive.google.com/drive/folders/1byo5cCWoeFP749_mLNXHeAfk_HO08H0-");

// var video = new BunnyVideoDrm(
//     // insert the referer between the quotes below (address of your webpage)
//     referer: "https://iframe.mediadelivery.net/5056fb0a-a739-416e-92af-acfa505e7b3a/playlist.drm?contextId=99959a4c-f523-4e30-ade5-710de730cad4&secret=1b0ccff3-d931-42e1-b7ce-656f347ec164",
//     // paste your embed link
//     embedUrl: "https://iframe.mediadelivery.net/embed/21030/5056fb0a-a739-416e-92af-acfa505e7b3a?autoplay=false&loop=true",
//     // you can override file name, no extension
//     name: "test2",
//     // you can override download path
//     path: @"./Temp");
//
// await video.Download();