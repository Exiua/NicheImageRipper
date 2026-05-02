using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.Managers;
using Core.Utility;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class XxxTubeParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "x-x-x";

    public XxxTubeParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<XxxTubeParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for x-x-x.tube and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        var parts = CurrentUrl.Split("/");
        var category = parts[3];
        var id = parts[4];
        string dirName;
        var images = new List<StringImageLinkWrapper>();
        switch (category)
        {
            case "videos":
            {
                var soup = await Soupify();
                dirName = soup.SelectSingleNodeOrThrow("//h1[@class='video-title']").InnerText.Split(",")[0].Trim() + $" ({id})";
                await WaitForElement("//div[@class='fp-ui']", timeout: 30);
                while (true)
                {
                    try
                    {
                        var ui = Driver.FindElement(By.XPath("//div[@class='fp-ui']"));
                        ui.Click();
                        break;
                    }
                    catch (UnknownErrorException)
                    {
                        // Delay needed to avoid Selenium's "tried to run command without establishing a connection" error
                        await Sleep(250);
                        CleanTabs("x-x-x.tube");
                    }
                }
                
                for (var i = 0; i < 4; i++)
                {
                    var element = await WaitForElement("//video", timeout: 30);
                    if (element is not null)
                    {
                        break;
                    }
                    
                    if (i == 3)
                    {
                        Logger.Error("Video element not found after waiting.");
                        throw new RipperException("Video element not found.");
                    }
                    
                    await Task.Delay(250);
                }
                
                var video = Driver.FindElement(By.XPath("//video"));
                var url = video.GetAttribute("src")!;
                images.Add(url);
                
                break;
            }
            case "albums":
            {
                await WaitForElement("//div[@id='albumGallery']//a", timeout: 100000);
                var lazyLoadArgs = new LazyLoadArgs()
                {
                    ScrollBy = true,
                    Increment = 1250,
                    ScrollPauseTime = 500
                };
                var soup = await Soupify(lazyLoadArgs: lazyLoadArgs);
                dirName = soup.SelectSingleNodeOrThrow("//div[@class='title']").InnerText.Split(",")[0].Trim() + $" ({id})";
                var list = soup.SelectSingleNodeOrThrow("//div[@class='holder']/div[@class='list']");
                var listItems = list.SelectNodesOrThrow("./div[@class='list-item']");
                var imageCount = listItems[3].InnerText.Trim().ParseInt();
                var imgs = soup.SelectSingleNodeOrThrow("//div[@id='albumGallery']")
                               .SelectNodesOrThrow("./div")
                               .Select(div => div.SelectSingleNodeOrThrow(".//a").GetHref())
                               .ToStringImageLinkWrapperList();
                if (imgs.Count != imageCount)
                {
                    Logger.Warning("Expected {Expected} images, but found {Found} images.", imageCount, imgs.Count);
                }
                
                images.AddRange(imgs);
                break;
            }
            default:
                throw new RipperException("Unknown category: " + category);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}