using System.Text.RegularExpressions;
using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using OpenQA.Selenium;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

public partial class NHentaiParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "nhentai";

    public NHentaiParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<NHentaiParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for nhentai.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250
        };
        await LazyLoad(lazyLoadArgs);
        var btn = Driver.TryFindElement(By.Id("show-all-images-button"));
        if (btn is not null)
        {
            Driver.ExecuteScript("arguments[0].scrollIntoView();", btn);
            btn.Click();
        }
        await LazyLoad(lazyLoadArgs);
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='title']").InnerText;
        var thumbnails = soup.SelectSingleNodeOrThrow("//div[@class='thumbs']")
                            .SelectNodesOrThrow(".//img")
                            .Select(img => img.GetNullableAttributeValue("data-src"))
                            .ToList();
        var images = thumbnails.Where(thumb => !string.IsNullOrEmpty(thumb)) // Remove nulls
                                .Select(thumb => NHentaiRegex().Replace(thumb!, "i7."))
                                .Select(newThumb => newThumb.Replace("t.", "."))
                                .ToStringImageLinkWrapperList();
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
    
    [GeneratedRegex(@"t\d\.")]
    private static partial Regex NHentaiRegex();
}
