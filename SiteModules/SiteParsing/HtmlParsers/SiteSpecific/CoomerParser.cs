using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.SiteParsing.HtmlParsers;
using OpenQA.Selenium;
using HtmlAgilityPack;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;

public class CoomerParser : DotPartyParser, IHtmlParser, IRefererOverrideHtmlParser
{
    public static string ParserName => "coomer";
    public static string[] SupportedUrls => ["https://coomer.party/", "https://coomer.su/", "https://coomer.st/"];
    public static string RefererOverride => "";

    public CoomerParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                        FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<CoomerParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for coomer.su and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override Task<RipInfo> ParseCore(CancellationToken cancellationToken = default)
    {
        return DotPartyParse("https://coomer.st", cancellationToken);
    }
}