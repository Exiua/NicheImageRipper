using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class LiveJasminBabesParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "livejasminbabes";

    public LiveJasminBabesParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<LiveJasminBabesParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for livejasminbabes.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        return await GenericBabesHtmlParser("//div[@id='gallery_header']//h1", "//div[@class='gallery_thumb']");
    }
}
