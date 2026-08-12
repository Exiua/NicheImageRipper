using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using OpenQA.Selenium;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;
public class XxxTubeParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "x-x-x";
    public static string[] SupportedUrls => ["https://x-x-x.tube/"];

    public XxxTubeParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<XxxTubeParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for x-x-x.tube and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var parts = CurrentUrl.Split("/");
        var category = parts[3];
        var id = parts[4];
        string dirName;
        var images = new List<StringFileLinkWrapper>();
        switch (category)
        {
            case "videos":
            {
                var soup = await Soupify(cancellationToken: cancellationToken);
                dirName = soup.SelectSingleNodeOrThrow("//h1[@class='video-title']").InnerText.Split(",")[0].Trim() + $" ({id})";
                await WaitForElement("//div[@class='fp-ui']", timeout: 30, cancellationToken: cancellationToken);
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
                        await Sleep(250, cancellationToken);
                        CleanTabs("x-x-x.tube");
                    }
                }

                for (var i = 0; i < 4; i++)
                {
                    var element = await WaitForElement("//video", timeout: 30, cancellationToken: cancellationToken);
                    if (element is not null)
                    {
                        break;
                    }

                    if (i == 3)
                    {
                        Logger.Error("Video element not found after waiting.");
                        throw new RipperException("Video element not found.");
                    }

                    await Task.Delay(250, cancellationToken);
                }

                var video = Driver.FindElement(By.XPath("//video"));
                var url = video.GetAttribute("src")!;
                images.Add(url);
                break;
            }

            case "albums":
            {
                await WaitForElement("//div[@id='albumGallery']//a", timeout: 100000, cancellationToken: cancellationToken);
                var lazyLoadArgs = new LazyLoadArgs()
                {
                    ScrollBy = true,
                    Increment = 1250,
                    ScrollPauseTime = 500
                };
                var soup = await Soupify(lazyLoadArgs: lazyLoadArgs, cancellationToken: cancellationToken);
                dirName = soup.SelectSingleNodeOrThrow("//div[@class='title']").InnerText.Split(",")[0].Trim() + $" ({id})";
                var list = soup.SelectSingleNodeOrThrow("//div[@class='holder']/div[@class='list']");
                var listItems = list.SelectNodesOrThrow("./div[@class='list-item']");
                var imageCount = listItems[3].InnerText.Trim().ParseInt();
                var imgs = soup.SelectSingleNodeOrThrow("//div[@id='albumGallery']").SelectNodesOrThrow("./div").Select(div => div.SelectSingleNodeOrThrow(".//a").GetHref()).ToStringImageLinkWrapperList();
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