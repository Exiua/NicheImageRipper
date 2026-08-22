using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using OpenQA.Selenium;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.MicMicDoll;
public class MicMicDollParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "micmicdoll";
    public static string[] SupportedUrls => ["https://sex.micmicdoll.com/"];

    public MicMicDollParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<MicMicDollParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for sex.micmicdoll.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        try
        {
            Driver.SwitchTo().Alert().Dismiss();
        }
        catch (NoAlertPresentException)
        {
        // ignored
        }

        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h3[@class='post-title entry-title']").InnerText;
        var images = soup.SelectNodesOrThrow("//div[@class='post-body entry-content']//a").Select(a => a.GetNullableHref()).Where(item => item is not null).Select(item => item!).ToStringFileLinkWrapperList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}