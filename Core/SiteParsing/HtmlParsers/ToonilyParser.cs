using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class ToonilyParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "toonily";

    public ToonilyParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for toonily.me and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='post-title']/h1")!.InnerText;
        var chapterList = soup.SelectSingleNode("//ul[@class='main version-chap no-volumn']")!
                              .SelectNodes("./li")!
                              .Select(li =>
                               {
                                   var a = li.SelectSingleNode("./a");
                                   var href = a!.GetHref();
                                   var text = a!.InnerText.Trim();
                                   return (href, text);
                               })
                              .Reverse();

        var images = new List<StringImageLinkWrapper>();
        foreach (var (chapter, name) in chapterList)
        {
            Log.Information("Parsing {ChapterName}", name);
            soup = await Soupify(chapter, lazyLoadArgs: new LazyLoadArgs { ScrollBy = true, Increment = 5000, ScrollPauseTime = 1000 });
            var imageList = soup.SelectSingleNode("//div[@class='reading-content']")!
                                .SelectNodes("./div")!
                                .Select(div => div.SelectSingleNode("./img"))
                                .Select(img => img!.GetSrc().Trim())
                                .ToStringImageLinks();
            
            images.AddRange(imageList);
        }
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
