

using NicheImageRipper.SiteModules.Modules.Av19a;
using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Avav19;
public class Avav19Parser : Av19aParser, IHtmlParser
{
    public new static string ParserName => "avav19";
    public new static string[] SupportedUrls => ["https://avav19.com/"];

    public Avav19Parser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<Avav19Parser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for avav19.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        return await base.Parse(cancellationToken: cancellationToken);
    }
}