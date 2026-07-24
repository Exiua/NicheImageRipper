using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class GoogleParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "google";

    public GoogleParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                        FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<GoogleParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Query the Google Drive API to get file information to download
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        return await GoogleParse("", cancellationToken);
    }

    /// <summary>
    ///     Query the Google Drive API to get file information to download
    /// </summary>
    /// <param name="gdriveUrl">The url to parse (default: CurrentUrl)</param>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> GoogleParse(string gdriveUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(gdriveUrl))
        {
            gdriveUrl = CurrentUrl;
        }

        // Actual querying happens within the RipInfo object
        return Task.FromResult(RipInfo.FromUrlList([gdriveUrl], "", FilenameScheme));
    }
}