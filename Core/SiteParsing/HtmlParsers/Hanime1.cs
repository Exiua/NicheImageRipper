using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class Hanime1 : HtmlParser, IHtmlParser
{
    public Hanime1(WebDriver driver, ApiClientManager apiClientManager, Dictionary<string, string> requestHeaders,
                   FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, apiClientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<Hanime1>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for site and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("").InnerText;
        var images = new List<StringImageLinkWrapper>();

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}