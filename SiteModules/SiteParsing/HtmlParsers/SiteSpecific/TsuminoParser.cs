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
public class TsuminoParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "tsumino";
    public static string[] SupportedUrls => ["https://www.tsumino.com/"];

    public TsuminoParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<TsuminoParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for tsumino.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='book-title']").InnerText;
        var numPages = int.Parse(soup.SelectSingleNodeOrThrow("//div[@id='Pages']").InnerText.Trim());
        var pagerUrl = CurrentUrl.Replace("/entry/", "/Read/Index/") + "?page=";
        var images = new List<StringFileLinkWrapper>();
        for (var i = 1; i <= numPages; i++)
        {
            soup = await Soupify($"{pagerUrl}{i}", delay: 3000);
            var src = soup.SelectSingleNodeOrThrow("//img[@class='img-responsive reader-img']").GetSrc().Replace("&amp;", "&");
            images.Add(src);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}