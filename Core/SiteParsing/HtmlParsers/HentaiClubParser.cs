using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class HentaiClubParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "hentaiclub";

    public HentaiClubParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for hentaiclub.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        await LazyLoad(new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250
        });
        
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//span[@class='post-info-text']").InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@id='masonry']")
                            .SelectNodesOrThrow("./div")
                            .Select(div => div.SelectSingleNodeOrThrow("./img").GetSrc())
                            .ToStringImageLinkWrapperList();
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
