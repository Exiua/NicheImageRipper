using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using OpenQA.Selenium;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class XMissyParser : HtmlParser
{
    public XMissyParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for xmissy.nl and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var loadButton = Driver.TryFindElement(By.Id("loadallbutton"));
        loadButton?.Click();
        await Task.Delay(1000);
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@id='pagetitle']")
                            .InnerText;
        var images = soup.SelectSingleNode("//div[@id='gallery']")
                            .SelectNodes(".//div[@class='noclick-image']")
                            .Select(img => img.SelectSingleNode(".//img").GetNullableSrc() ?? img.SelectSingleNode(".//img").GetSrc())
                            .ToStringImageLinkWrapperList();
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
