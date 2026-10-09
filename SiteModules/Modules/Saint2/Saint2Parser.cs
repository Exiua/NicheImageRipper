using NicheImageRipper.Sdk.Common.ExtensionMethods;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.Exceptions;
using NicheImageRipper.Sdk.SiteParsing;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Saint2;

public class Saint2Parser : ParameterizedHtmlParser
{
    public Saint2Parser(WebDriver driver, Dictionary<string, string> requestHeaders,
                        FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,
        requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses  the HTML for site and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> ParseCore(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        string dirName;
        var images = new List<StringFileLinkWrapper>();
        if (CurrentUrl.Contains("/embed/"))
        {
            dirName = "Saint2 Video";
            var downloadLink = soup.SelectSingleNodeOrThrow("//a[@class='plyr__controls__item plyr__control']")
                                   .GetHref();
            soup = await Soupify(downloadLink, cancellationToken: cancellationToken);
            var link = soup.SelectSingleNodeOrThrow("//a").GetHref();
            images.Add(link);
        }
        else
        {
            throw new RipperException($"Unhandled url: {CurrentUrl}");
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}