using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using OpenQA.Selenium;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class XMissyParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "xmissy";

    public XMissyParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for xmissy.nl and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        var loadButton = Driver.TryFindElement(By.Id("loadallbutton"));
        loadButton?.Click();
        await Sleep(1000);
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@id='pagetitle']")
                            .InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@id='gallery']")
                            .SelectNodesOrThrow(".//div[@class='noclick-image']")
                            .Select(img => img.SelectSingleNode(".//img")?.GetNullableSrc() ?? img.SelectSingleNodeOrThrow(".//img").GetSrc())
                            .ToStringImageLinkWrapperList();
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
