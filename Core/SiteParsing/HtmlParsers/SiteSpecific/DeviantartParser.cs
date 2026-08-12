using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

public class DeviantartParser : HtmlParser
{
    public static string ParserName => "deviantart";
    public static string[] SupportedUrls { get; } = ["https://www.deviantart.com/"];
    
    public DeviantartParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                            FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses  the HTML for deviantart.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = CurrentUrl.Split("/")[3];

        //var images = new List<StringFileLinkWrapper>();
        // TODO: Implement the rest of the method
        return RipInfo.ForExternalTool(CurrentUrl, dirName, FilenameScheme);
    }
}