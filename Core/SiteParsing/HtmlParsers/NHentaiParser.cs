using Common.ExtensionMethods;
using System.Text.RegularExpressions;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using OpenQA.Selenium;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public partial class NHentaiParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "nhentai";

    public NHentaiParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<NHentaiParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for nhentai.net and extracts the relevant information necessary for downloading images from the site
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
