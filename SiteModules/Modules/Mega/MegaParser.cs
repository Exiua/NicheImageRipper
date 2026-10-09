using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Mega;

public class MegaParser : ParameterizedHtmlParser, IHtmlParser
{
    public static string ParserName => "mega";
    public static string[] SupportedUrls { get; } = ["https://mega.nz/"];
    protected override bool RequiresNavigation => false;

    public MegaParser(WebDriver driver, Dictionary<string, string> requestHeaders,
                      FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders,
        IHtmlParser.GetFilenameScheme<MegaParser>(filenameScheme))
    {
    }

    protected override Task<RipInfo> ParseCore(CancellationToken cancellationToken = default)
    {
        var fileLink = FileLink.Create(GivenUrl, FilenameScheme, linkInfo: MegaLinkInfo.Mega);
        var dirName = GivenUrl.Split('/')[4];
        return Task.FromResult(RipInfo.FromUrlList([fileLink], dirName, FilenameScheme));
    }
}