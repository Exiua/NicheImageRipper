using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.Managers;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class XsnvshenParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "xsnvshen";

    public XsnvshenParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<XsnvshenParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for xsnvshen.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        var referer = CurrentUrl.Split("/").Take(5).Join("/");
        var dirName = Driver.FindElement(By.XPath("//h1/a")).Text;
        var pageCount = Driver.FindElement(By.XPath("//em[@id='time']/span"))
                                .Text
                                .Split(" ")[1]
                                .ParseInt();
        var images = new List<StringImageLinkWrapper>();
        for (var i = 0; i < pageCount; i++)
        {
            Logger.Information("Parsing page {Page}", i + 1);
            var img = Driver.FindElement(By.XPath("//img[@id='bigImg']"))
                            .GetAttribute("src");
            if (img is null)
            {
                throw new RipperException("Unable to find image on page " + (i + 1));
            }
            
            images.Add(img);
            if (i < pageCount - 1)
            {
                Driver.FindElement(By.XPath("//span[@id='next']")).Click();
                await Sleep(1000);
            }
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme, referer: referer);
    }
}