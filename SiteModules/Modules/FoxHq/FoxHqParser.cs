

using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.FoxHq;
public class FoxHqParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "foxhq";
    public static string[] SupportedUrls => ["https://www.foxhq.com/"];

    public FoxHqParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<FoxHqParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for foxhq.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h1").InnerText;
        if (string.IsNullOrEmpty(dirName))
        {
            dirName = soup.SelectSingleNodeOrThrow("//h2").InnerText;
        }

        var url = CurrentUrl;
        var images = soup.SelectNodesOrThrow("//div[@class='thumb simple']").Select(img => img.SelectSingleNodeOrThrow(".//a").GetHref()).ToStringFileLinkWrapperList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}