using System.Text.RegularExpressions;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using OpenQA.Selenium;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public partial class NHentaiParser : HtmlParser
{
    public NHentaiParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for nhentai.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
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
        var dirName = soup.SelectSingleNode("//h1[@class='title']").InnerText;
        var thumbnails = soup.SelectSingleNode("//div[@class='thumbs']")
                            .SelectNodes(".//img")
                            .Select(img => img.GetNullableAttributeValue("data-src"))
                            .ToList();
        var images = thumbnails.Where(thumb => !string.IsNullOrEmpty(thumb)) // Remove nulls
                                .Select(thumb => NHentaiRegex().Replace(thumb!, "i7."))
                                .Select(newThumb => newThumb.Replace("t.", "."))
                                .ToStringImageLinkWrapperList();
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    [GeneratedRegex(@"t\d\.")]
    private static partial Regex NHentaiRegex();
}
