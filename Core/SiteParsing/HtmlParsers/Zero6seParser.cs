using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using HtmlAgilityPack;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class Zero6SeParser : HtmlParser
{
    public Zero6SeParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for 06se.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        // var readMoreButton = Driver.TryFindElement(By.XPath("//div[@class='read-more']/a"));
        // readMoreButton?.Click();
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='article-title']/a").InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@class='article-content']")
                         .SelectNodesOrThrow(".//img")
                         .Select(GetUrl)
                         .OfType<string>()
                         .ToStringImageLinkWrapperList();

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    private static string? GetUrl(HtmlNode img)
    {
        var src = img.GetNullableAttributeValue("data-src");
        if (src is not null)
        {
            return src;
        }

        src = img.GetNullableSrc();
        if (string.IsNullOrWhiteSpace(src))
        {
            return null;
        }
        
        return src;
    }
}