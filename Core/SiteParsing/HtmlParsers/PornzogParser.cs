using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class PornzogParser : HtmlParser
{
    public PornzogParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for pornzog.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        var id = CurrentUrl.Split("/")[4];
        var dirName = Driver.FindElement(By.XPath("//h2[@class='title video-title']")).Text + $" ({id})";
        var iframe = Driver.FindElement(By.XPath("//iframe[@allowfullscreen]"));
        var iframeSrc = iframe.GetAttribute("src")!;
        var baseUrl = iframeSrc.Split("/").Take(3).Join("/");
        Driver.SwitchTo().Frame(iframe);
        var soup = await Soupify();
        var images = new List<StringImageLinkWrapper>();
        var url = soup.SelectSingleNodeOrThrow("//video[@class='jw-video jw-reset']").GetSrc();
        images.Add(baseUrl + url);

        return RipInfo.FromUrlList(images, dirName, FilenameScheme, referer: null);
    }
}