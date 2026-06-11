using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class FiveGeParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "5ge";

    public FiveGeParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<FiveGeParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for happy.5ge.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250,
            ScrollPauseTime = 1000
        });
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='joe_detail__title']").InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@class='joe_gird']")
                            .SelectNodesOrThrow(".//img")
                            .Select(img => img.GetSrc())
                            .ToStringImageLinkWrapperList();
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
