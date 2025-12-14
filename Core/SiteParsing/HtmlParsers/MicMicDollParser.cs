using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using OpenQA.Selenium;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class MicMicDollParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "micmicdoll";

    public MicMicDollParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for sex.micmicdoll.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
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
        var dirName = soup.SelectSingleNodeOrThrow("//h3[@class='post-title entry-title']").InnerText;
        var images = soup.SelectNodesOrThrow("//div[@class='post-body entry-content']//a")
                            .Select(a => a.GetNullableHref())
                            .Where(item => item is not null)
                            .Select(item => item!)
                            .ToStringImageLinkWrapperList();
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
