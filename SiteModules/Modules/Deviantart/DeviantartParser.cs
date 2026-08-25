

using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Deviantart;

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
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = CurrentUrl.Split("/")[3];

        //var images = new List<StringFileLinkWrapper>();
        // TODO: Implement the rest of the method
        return RipInfo.ForExternalTool(CurrentUrl, dirName, FilenameScheme);
    }
}