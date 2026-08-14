using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;

public class Saint2Parser : ParameterizedHtmlParser
{
    public Saint2Parser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                        FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
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