using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using OpenQA.Selenium;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class MicMicDollParser : HtmlParser
{
    public MicMicDollParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for sex.micmicdoll.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        try
        {
            Driver.SwitchTo().Alert().Dismiss();
        }
        catch (NoAlertPresentException)
        {
            // ignored
        }
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h3[@class='post-title entry-title']").InnerText;
        var images = soup.SelectNodes("//div[@class='post-body entry-content']//a")
                            .Select(a => a.GetNullableHref())
                            .Where(item => item is not null)
                            .Select(item => item!)
                            .ToStringImageLinkWrapperList();
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
