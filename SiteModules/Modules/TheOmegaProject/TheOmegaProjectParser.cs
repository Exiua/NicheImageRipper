using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.TheOmegaProject;
public class TheOmegaProjectParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "theomegaproject";
    public static string[] SupportedUrls => ["https://www.theomegaproject.org/"];

    public TheOmegaProjectParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<TheOmegaProjectParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for theomegaproject.org and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectNodesOrThrow("//h2[@class='section-title title']")[1].InnerText.Split("Porn")[0].Split("porn")[0].Trim();
        var images = soup.SelectSingleNodeOrThrow("//div[@class='lightgallery thumbs quadruple fivefold']").SelectNodesOrThrow(".//img").Select(img => img.GetSrc()).ToStringFileLinkWrapperList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}