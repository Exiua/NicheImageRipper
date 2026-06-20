using Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class ThreeHentaiParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "3hentai";

    public ThreeHentaiParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<ThreeHentaiParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for 3hentai.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250,
        };
        var soup = await Soupify(lazyLoadArgs: lazyLoadArgs);
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='text-left font-weight-bold']").InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@id='thumbnail-gallery']")
                         .SelectNodesOrThrow("./div")
                         .Select(div => div.SelectSingleNodeOrThrow(".//img").GetSrc())
                         .Select(src => src.Replace("t.", "."))
                         .ToStringImageLinkWrapperList();

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}