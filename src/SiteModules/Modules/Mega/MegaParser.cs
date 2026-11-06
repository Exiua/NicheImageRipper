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

    /// <summary>
    ///     Names the gallery after the shared folder (or file) in MEGA. The link itself is expanded into one link per
    ///     file by <see cref="MegaLinkRule" />, keeping the folder structure.
    /// </summary>
    protected override async Task<RipInfo> ParseCore(CancellationToken cancellationToken = default)
    {
        var link = MegaHelper.Parse(GivenUrl);
        var name = await MegaHelper.GetNameAsync(GivenUrl, cancellationToken);
        var dirName = link.Kind == MegaLinkKind.Folder ? name : Path.GetFileNameWithoutExtension(name);
        return await RipInfo.FromUrlListAsync([GivenUrl], dirName, FilenameScheme);
    }
}
