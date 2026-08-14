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
public class JRantsParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "jrants";
    public static string[] SupportedUrls => ["https://en.jrants.com/"];

    public JRantsParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<JRantsParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for jrants.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250
        };
        var soup = await Soupify(lazyLoadArgs: lazyLoadArgs);
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='entry-title']").InnerText;
        var images = new List<StringFileLinkWrapper>();
        var pageCount = 1;
        var noImagesFound = false;
        while (true)
        {
            Logger.Information("Parsing page {pageCount}", pageCount);
            pageCount++;
            var imgs = soup.SelectNodes("//div[@class='inside-article']//p/img")?.Select(img => img.GetSrc()).ToStringImageLinks();
            if (imgs is not null)
            {
                images.AddRange(imgs);
            }
            else
            {
                noImagesFound = true;
            }

            var pagination = soup.SelectSingleNode("//div[@class='pgntn-page-pagination-block']")?.SelectNodes("./a");
            var nextPage = pagination?.FirstOrDefault(a => a.InnerText.StartsWith("Next"));
            if (nextPage is null)
            {
                break;
            }

            // Some albums don't have images on the last page, so if we find no images and there is a next page, we throw an exception
            if (noImagesFound)
            {
                throw new RipperException("No images found");
            }

            soup = await Soupify(nextPage.GetHref(), lazyLoadArgs: lazyLoadArgs);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}