using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class FoxHqParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "foxhq";

    public FoxHqParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<FoxHqParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for foxhq.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1").InnerText;
        if (string.IsNullOrEmpty(dirName))
        {
            dirName = soup.SelectSingleNodeOrThrow("//h2").InnerText;
        }
    
        var url = CurrentUrl;
        var images = soup
                    .SelectNodesOrThrow("//div[@class='thumb simple']")
                    .Select(img => img.SelectSingleNodeOrThrow(".//a").GetHref())
                    .ToStringImageLinkWrapperList();
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
