using NicheImageRipper.Sdk.Common.ExtensionMethods;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.MediaFire;

public class MediaFireParser : ParameterizedHtmlParser, IHtmlParser
{
    public static string ParserName => "mediafire";
    public static string[] SupportedUrls { get; } = ["https://www.mediafire.com/"];

    public MediaFireParser(WebDriver driver, Dictionary<string, string> requestHeaders,
                           FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders,
        IHtmlParser.GetFilenameScheme<MediaFireParser>(filenameScheme))
    {
    }

    /// <inheritdoc />
    protected override async Task<RipInfo> ParseCore(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        string dirName;
        List<StringFileLinkWrapper> fileLinks = [];
        if (CurrentUrl.Contains("/file/"))
        {
            dirName = soup.SelectSingleNodeOrThrow("//div[@class='filename']").InnerText;
            var fileLink = soup.SelectSingleNodeOrThrow("//a[@id='downloadButton']").GetHref();
            fileLinks.Add(fileLink);
        }
        else
        {
            throw new NotImplementedException();
        }
        
        return await RipInfo.FromUrlListAsync(fileLinks, dirName, FilenameScheme);
    }
}