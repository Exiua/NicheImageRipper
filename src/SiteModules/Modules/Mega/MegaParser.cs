using MegaSdk;
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
    ///     Names the gallery after the shared folder (or file) in MEGA and expands the link into one link per file,
    ///     keeping the folder structure. A link that can't be listed fails the rip, except that a link that's gone
    ///     (deleted, expired or taken down) parses to nothing when another parser handed it in, so one dead link
    ///     doesn't fail that parser's rip.
    /// </summary>
    protected override async Task<RipInfo> ParseCore(CancellationToken cancellationToken = default)
    {
        try
        {
            return await ParseLinkAsync(cancellationToken);
        }
        catch (MegaException e) when (IsSubParserCall && MegaHelper.IsUnavailable(e.ErrorCode))
        {
            Logger.Warning("Mega link is no longer available ({ErrorCode}: {Error}), skipping it: {Url}", e.ErrorCode,
                e.Message, GivenUrl);
            return RipInfo.Empty;
        }
    }

    private async Task<RipInfo> ParseLinkAsync(CancellationToken cancellationToken)
    {
        var link = MegaHelper.Parse(GivenUrl);
        var name = await MegaHelper.GetNameAsync(GivenUrl, cancellationToken);
        var dirName = link.Kind == MegaLinkKind.Folder ? name : Path.GetFileNameWithoutExtension(name);
        var (fileLinks, _) = await MegaHelper.ExpandAsync(GivenUrl, 0, FilenameScheme, cancellationToken);
        return await RipInfo.FromUrlListAsync([..fileLinks.Select(fileLink => (StringFileLinkWrapper)fileLink)],
            dirName, FilenameScheme);
    }
}
