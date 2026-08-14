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
public class SexyKittenPornParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "sexykittenporn";
    public static string[] SupportedUrls => ["https://www.sexykittenporn.com/"];

    public SexyKittenPornParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<SexyKittenPornParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for sexykittenporn.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='blockheader']").InnerText;
        var tagList = soup.SelectNodesOrThrow("//div[@class='list gallery col3']").SelectMany(tag => tag.SelectNodesOrThrow(".//div[@class='item']"));
        var imageLink = tagList.Select(image => $"https://www.sexykittenporn.com{image.SelectSingleNodeOrThrow(".//a").GetHref()}");
        var images = new List<StringFileLinkWrapper>();
        foreach (var link in imageLink)
        {
            soup = await Soupify(link);
            images.Add($"https:{soup.SelectSingleNodeOrThrow("//div[@class='image-wrapper']//img").GetSrc()}");
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}