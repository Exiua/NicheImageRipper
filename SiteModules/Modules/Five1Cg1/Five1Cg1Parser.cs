using NicheImageRipper.Sdk.Common.ExtensionMethods;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Five1Cg1;

public class Five1Cg1Parser : HtmlParser, IHtmlParser
{
    public static string ParserName => "51cg1";
    public static string[] SupportedUrls => ["https://51cg1.com/"];

    public Five1Cg1Parser(WebDriver driver, Dictionary<string, string> requestHeaders,
                          FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,
        requestHeaders, IHtmlParser.GetFilenameScheme<Five1Cg1Parser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for 51cg1.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h1[contains(@class, 'post-title')]").InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@class='post-content']").SelectNodesOrThrow(".//img")
                         .Select(img => img.GetSrc()).ToStringFileLinkWrapperList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}