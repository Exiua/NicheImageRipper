using Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using OpenQA.Selenium;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class XasiatParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "xasiat";

    public XasiatParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<XasiatParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for xasiat.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250,
        });
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='headline']/h1").InnerText;
        List<StringImageLinkWrapper> images;
        if (CurrentUrl.Contains("/albums/"))
        {
            images = soup.SelectSingleNodeOrThrow("//div[@class='images']")
                            .SelectNodesOrThrow("./a")
                            .Select(a => a.GetHref())
                            .ToStringImageLinkWrapperList();
        }
        else if (CurrentUrl.Contains("/videos/"))
        {
            var playButton = Driver.FindElement(By.XPath("//a[@class='fp-play']"));
            Driver.ScrollElementIntoView(playButton);
            Driver.Click(playButton);
            var waitedElement = await WaitForElement("//video");
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
            
            await Sleep(1000);
            var video = Driver.TryFindElement(By.XPath("//video"));
            var src = video!.GetSrc()!;
            images = [src];
        }
        else
        {
            throw new RipperException("Unknown URL type");
        }
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
