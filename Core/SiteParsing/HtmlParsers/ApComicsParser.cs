using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class ApComicsParser : HtmlParser
{
    public ApComicsParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "",
                                FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for apcomics.org and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='post-title']/h1").InnerText;
        var images = new List<StringImageLinkWrapper>();
        var chapters = soup.SelectSingleNode("//ul[@class='main version-chap no-volumn']")
                            .SelectNodes("./li")
                            .Select(li => li.SelectSingleNode("./a").GetHref())
                            .Reverse();
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true
        };
        foreach (var chapter in chapters)
        {
            Log.Debug("Parsing chapter {Chapter}", chapter);
            soup = await Soupify(chapter, lazyLoadArgs: lazyLoadArgs);
            var imgs = soup.SelectSingleNode("//div[@class='reading-content']")
                           .SelectNodes("./div")
                           .Select(div => div.SelectSingleNode("./img").GetSrc().Trim())
                           .ToStringImageLinks();
            images.AddRange(imgs);
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }
}