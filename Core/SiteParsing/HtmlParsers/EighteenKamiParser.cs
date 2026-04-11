using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class EighteenKamiParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "18kami";

    public EighteenKamiParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<EighteenKamiParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for 18kami.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        var url = CurrentUrl.Split("/")[..5].Join("/").Replace("/album/", "/photo/");
        var soup = await Soupify(url, lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250
        });
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='panel-heading']/div[@class='pull-left']").InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@class='row thumb-overlay-albums']")
                            .SelectNodesOrThrow(".//img")
                            .Select(img => $"https://18kami.com{img.GetSrc()}")
                            .ToStringImageLinkWrapperList();
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
