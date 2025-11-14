using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class ApComicsParser : HtmlParser
{
    public ApComicsParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for apcomics.org and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        await WaitForElement("//ul[@class='main version-chap no-volumn']/li/a", timeout: 60);
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='post-title']/h1").InnerText;
        var images = new List<StringImageLinkWrapper>();
        var chapters = soup.SelectSingleNodeOrThrow("//ul[@class='main version-chap no-volumn']")
                           .SelectNodesOrThrow("./li")
                           .Select(li => li.SelectSingleNodeOrThrow("./a").GetHref())
                           .Reverse();
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true
        };
        foreach (var chapter in chapters)
        {
            Log.Debug("Parsing chapter {Chapter}", chapter);
            soup = await Soupify(chapter, lazyLoadArgs: lazyLoadArgs);
            var imgs = soup.SelectSingleNodeOrThrow("//div[@class='reading-content']")
                           .SelectNodesOrThrow("./div")
                           .Select(div => div.SelectSingleNodeOrThrow("./img").GetSrc().Trim())
                           .ToStringImageLinks();
            images.AddRange(imgs);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}