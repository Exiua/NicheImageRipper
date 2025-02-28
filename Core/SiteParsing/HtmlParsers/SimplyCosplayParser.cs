using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using OpenQA.Selenium;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class SimplyCosplayParser : HtmlParser
{
    public SimplyCosplayParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for simply-cosplay.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        await Task.Delay(5000);
        var viewButton = Driver.TryFindElement(By.XPath("//button[@class='btn btn-default']"));
        viewButton?.Click();
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='content-headline']").InnerText;
        var imageList = soup.SelectSingleNode("//div[@class='swiper-wrapper']");
        List<StringImageLinkWrapper> images;
        if (imageList is null)
        {
            images = [soup.SelectSingleNode("//div[@class='image-wrapper']//img")
                            .GetSrc()];
        }
        else
        {
            images = soup.SelectSingleNode("//section/div[@class='row vertical-gutters']")
                            .SelectNodes(".//img")
                            .Select(url => url.GetAttributeValue("data-src").Remove("thumb_"))
                            .ToStringImageLinkWrapperList();
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
