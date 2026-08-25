
using NicheImageRipper.Core.Exceptions;

using OpenQA.Selenium;
using Sdk.Common.ExtensionMethods;
using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Xasiat;
public class XasiatParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "xasiat";
    public static string[] SupportedUrls => ["https://www.xasiat.com/"];

    public XasiatParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = Sdk.Enums.FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<XasiatParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for xasiat.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs { ScrollBy = true, Increment = 1250, }, cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='headline']/h1").InnerText;
        List<StringFileLinkWrapper> images;
        if (CurrentUrl.Contains("/albums/"))
        {
            images = soup.SelectSingleNodeOrThrow("//div[@class='images']").SelectNodesOrThrow("./a").Select(a => a.GetHref()).ToStringFileLinkWrapperList();
        }
        else if (CurrentUrl.Contains("/videos/"))
        {
            var playButton = Driver.FindElement(By.XPath("//a[@class='fp-play']"));
            Driver.ScrollElementIntoView(playButton);
            Driver.Click(playButton);
            var waitedElement = await WaitForElement("//video", cancellationToken: cancellationToken);
            if (waitedElement is null)
            {
                throw new RipperException("Video not found");
            }

            var player = Driver.FindElement(By.Id("kt_player"));
            var qualityButton = Driver.TryFindElement(By.XPath("//a[@class='fp-settings']"));
            if (qualityButton is not null)
            {
                var classes = player.GetDomAttribute("class")!;
                classes += " is-settings-open";
                Driver.ExecuteScript($"document.getElementById('kt_player').setAttribute('class', '{classes}')");
                var bestQuality = Driver.TryFindElement(By.XPath("//div[@class='fp-settings-list-item is-hd']/a"));
                if (bestQuality is not null)
                {
                    Driver.Click(bestQuality);
                }
                else
                {
                    Logger.Warning("No HD quality found, using default");
                }
            }

            await Sleep(1000, cancellationToken: cancellationToken);
            var video = Driver.TryFindElement(By.XPath("//video"));
            var src = video!.GetSrc()!;
            images = [src];
        }
        else
        {
            throw new RipperException("Unknown URL type");
        }

        return RipInfo.FromUrlList(images, dirName, Sdk.Enums.FilenameScheme);
    }
}