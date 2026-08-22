using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using OpenQA.Selenium;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.HustleBootyTempTats;
public class HustleBootyTempTatsParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "hustlebootytemptats";
    public static string[] SupportedUrls => ["https://hustlebootytemptats.com/"];

    public HustleBootyTempTatsParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<HustleBootyTempTatsParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for hustlebootytemptats.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var pauseButton = Driver.TryFindElement(By.XPath("//div[@class='galleria-playback-button pause']"));
        pauseButton?.Click();
        var soup = await Soupify(delay: 1000, cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='zox-post-title left entry-title']").InnerText;
        var imagesNode = soup.SelectNodes("//div[@class='galleria-thumbnails']//img");
        List<StringFileLinkWrapper> images;
        if (imagesNode is not null)
        {
            images = imagesNode.Select(img => img.GetSrc().Remove("/cache").Split("-nggid")[0]).ToStringFileLinkWrapperList();
        }
        else
        {
            var nextButton = Driver.TryFindElement(By.XPath("//div[@class='galleria-image-nav-right']"));
            if (nextButton is not null)
            {
                images = [];
                var seen = new HashSet<string>();
                var newImages = true;
                while (newImages)
                {
                    var imageNodes = Driver.FindElements(By.XPath("//div[@class='galleria-image']//img"));
                    newImages = false;
                    foreach (var imageNode in imageNodes)
                    {
                        var src = imageNode.GetSrc();
                        if (!seen.Add(src))
                        {
                            continue;
                        }

                        newImages = true;
                        images.Add(src);
                    }

                    nextButton.Click();
                    await Sleep(1000, cancellationToken);
                }
            }
            else
            {
                var iframe = soup.SelectSingleNodeOrThrow("//iframe");
                var iframeUrl = iframe.GetSrc();
                images = [iframeUrl]; // YouTube video (probably)
            }
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}