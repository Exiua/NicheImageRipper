using NicheImageRipper.Sdk.Common.ExtensionMethods;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.ThotsBay;

public class ThotsBayParser : HtmlParser
{
    public ThotsBayParser(WebDriver driver, Dictionary<string, string> requestHeaders,
                          FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders,
        filenameScheme)
    {
    }

    /// <summary>
    ///     Parses  the HTML for thotsbay.tv and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250,
        };
        var soup = await Soupify(lazyLoadArgs: lazyLoadArgs, cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='actor-name']/h1").InnerText;
        var container = soup.SelectSingleNodeOrThrow("//div[@id='media-items-all']");
        var items = container.SelectNodesOrThrow("./div")
                             .Select(div => $"https://thotsbay.tv{div.SelectSingleNodeOrThrow(".//a").GetHref()}");
        var images = new List<StringFileLinkWrapper>();
        foreach (var item in items)
        {
            soup = await Soupify(item, delay: 250, cancellationToken: cancellationToken);
        }

        // Unable to download videos from blob, parse in on hold
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}