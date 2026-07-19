using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using OpenQA.Selenium;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class NoodleMagazineParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "noodlemagazine";

    public NoodleMagazineParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<NoodleMagazineParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for noodlemagazine.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await SolveParseAddCookies();
        var images = new List<StringFileLinkWrapper>();
        string dirName;
        if (CurrentUrl.Contains("/watch/"))
        {
            dirName = soup.SelectSingleNodeOrThrow("//div[@class='l_info']/h1").InnerText;
            var src = await GetVideoUrl();
            if (src is null)
            {
                Logger.Warning("Video has been deleted: {Url}", CurrentUrl);
                return RipInfo.Empty.WithDirectoryName(dirName);
            }
            
            images.Add(src);
        }
        else
        {
            dirName = soup.SelectSingleNodeOrThrow("//h1[@class='c_title']").InnerText;
            var videos = soup.SelectSingleNodeOrThrow("//div[@id='list_videos']")
                             .SelectNodesOrThrow("./div")
                             .Select(div => div.SelectSingleNodeOrThrow("./a").GetHref());
            foreach (var video in videos)
            {
                var url = $"https://noodlemagazine.com{video}";
                CurrentUrl = url;
                var src = await GetVideoUrl();
                if (src is null)
                {
                    Logger.Warning("Video has been deleted: {Url}", url);
                    continue;
                }

                images.Add(src);
            }
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    private async Task<string?> GetVideoUrl(CancellationToken cancellationToken = default)
    {
        start:
        await SolveParseAddCookies();
        var deletedAlert = Driver.TryFindElement(By.XPath("//div[@class='alert_warning']"));
        if (deletedAlert is not null)
        {
            return null;
        }

        (int Play, int Quality) tries = (0, 0);
        // TODO: Remove this infinite loop; refactor to use retries
        while (true)
        {
            Logger.Debug("Starting video");
            var success =
                await TryClickElementByXPath(
                    "//div[@class='jw-icon jw-icon-display jw-button-color jw-reset']");
            if (!success)
            {
                Logger.Debug("Could not find play button, retrying");
                tries.Play++;
                if (tries.Play > 3)
                {
                    Logger.Debug("Could not find play button, giving up");
                    tries.Play = 0;
                }
                else
                {
                    continue;
                }
            }

            await Sleep(250);

            Logger.Debug("Finding settings button");
            success = await TryClickElementByXPath(
                "//div[@class='jw-icon jw-icon-inline jw-button-color jw-reset jw-icon-settings jw-settings-submenu-button']");
            if (!success)
            {
                Logger.Debug("Could not find settings button, retrying");
                continue;
            }

            await Sleep(250);

            Logger.Debug("Finding highest quality button");
            success = await TryClickElementByXPath(
                "//div[@id='jw-player_box-settings-submenu-quality']//button");
            if (!success)
            {
                tries.Quality++;
                if (tries.Quality > 3)
                {
                    Logger.Debug("Could not find highest quality button, giving up");
                    break;
                }

                Logger.Debug("Could not find highest quality button, retrying");
                continue;
            }

            await Sleep(250);

            break;
        }

        var soup = await Soupify();
        var vid = soup.SelectSingleNodeOrThrow("//video[@class='jw-video jw-reset']");
        var src = vid.GetSrc().DecodeUrl();
        if (!src.Contains("rs=") && !src.Contains("url="))
        {
            Logger.Warning("Video link does not contain rs=, retrying: {Src}", src);
            Driver.Refresh();
            goto start; // TODO: Figure out a better way to handle this
        }

        return src;
    }

    private async Task<bool> TryClickElementByXPath(string xpath, int msDelay = 250, CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < 4; i++)
        {
            var element = Driver.TryFindElement(By.XPath(xpath));
            if (element is not null)
            {
                try
                {
                    element.Click();
                }
                catch (ElementNotInteractableException)
                {
                    Driver.Click(element);
                }

                return true;
            }

            Logger.Debug("Element '{XPath}' not found, retrying in {msDelay}ms", xpath, msDelay);
            await Task.Delay(msDelay);
        }

        return false;
    }
}