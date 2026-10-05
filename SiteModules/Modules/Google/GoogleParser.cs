using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Google;

public class GoogleParser : ParameterizedHtmlParser, IHtmlParser
{
    public static string ParserName => "google";
    public static string[] SupportedUrls => ["https://drive.google.com/"];
    protected override bool RequiresNavigation => false;

    public GoogleParser(WebDriver driver,  Dictionary<string, string> requestHeaders,
                        FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, 
        requestHeaders, IHtmlParser.GetFilenameScheme<GoogleParser>(filenameScheme))
    {
    }

    /// <inheritdoc />
    protected override Task<RipInfo> ParseCore(CancellationToken cancellationToken = default)
    {
        // Actual querying happens within the RipInfo object
        return Task.FromResult(RipInfo.FromUrlList([GivenUrl], "", FilenameScheme));
    }
}