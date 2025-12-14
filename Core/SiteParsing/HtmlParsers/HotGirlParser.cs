using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class HotGirlParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "hotgirl";

    public HotGirlParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for hotgirl.asia and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        if (!CurrentUrl.Contains("stype=slideshow"))
        {
            var urlParts = CurrentUrl.Split("/")[..4];
            CurrentUrl = "/".Join(urlParts) + "/?stype=slideshow";
        }
        
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h3[@itemprop='name']").InnerText;
        var images = soup.SelectNodesOrThrow("//img[@class='center-block w-100']")
                         .Select(image => image.GetSrc())
                         .ToStringImageLinkWrapperList();
        
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
