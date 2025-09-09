using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.Utility;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class XxxTubeParser : HtmlParser
{
    public XxxTubeParser(WebDriver driver, Dictionary<string, string> requestHeaders,
                         FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders,
        filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for x-x-x.tube and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var parts = CurrentUrl.Split("/");
        var category = parts[3];
        var id = parts[4];
        string dirName;
        var soup = await Soupify();
        var images = new List<StringImageLinkWrapper>();
        switch (category)
        {
            case "videos":
            {
                dirName = soup.SelectSingleNodeOrThrow("//h1[@class='video-title']").InnerText + $" ({id})";
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
                        Log.Error("Video element not found after waiting.");
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
                dirName = soup.SelectSingleNodeOrThrow("//div[@class='title']").InnerText + $" ({id})";
                var list = soup.SelectSingleNodeOrThrow("//div[@class='holder']/div[@class='list']");
                var listItems = list.SelectNodesOrThrow("./div[@class='list-item']");
                var imageCount = listItems[3].InnerText.Trim().ToInt();
                var imgs = soup.SelectSingleNodeOrThrow("//div[@id='albumGallery']")
                               .SelectNodesOrThrow("./div")
                               .Select(div => div.SelectSingleNodeOrThrow(".//a").GetHref())
                               .ToStringImageLinkWrapperList();
                if (imgs.Count != imageCount)
                {
                    Log.Warning("Expected {Expected} images, but found {Found} images.", imageCount, imgs.Count);
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