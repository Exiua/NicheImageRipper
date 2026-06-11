using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class LeakedBbParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "leakedbb";

    public LeakedBbParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<LeakedBbParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for leakedbb.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='flow-text left']")
                            .SelectSingleNodeOrThrow(".//h1")
                            .InnerText;
        var imageLinks = soup.SelectSingleNodeOrThrow("//div[@class='post_body scaleimages']")
                            .SelectNodesOrThrow("./img")
                            .Select(img => img.GetSrc())
                            .ToList();
        var images = new List<StringImageLinkWrapper>();
        foreach (var link in imageLinks)
        {
            if (!link.Contains("postimg.cc"))
            {
                images.Add(link);
                continue;
            }
    
            soup = await Soupify(link);
            var img = soup.SelectSingleNodeOrThrow("//a[@id='download']").GetHref().Split("?")[0];
            images.Add(img);
        }
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
