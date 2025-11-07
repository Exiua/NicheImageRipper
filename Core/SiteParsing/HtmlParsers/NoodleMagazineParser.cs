using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class NoodleMagazineParser : HtmlParser
{
    public NoodleMagazineParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for noodlemagazine.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await SolveParseAddCookies();
        var images = new List<StringImageLinkWrapper>();
        string dirName;
        if (CurrentUrl.Contains("/watch/"))
        {
            dirName = soup.SelectSingleNode("//div[@class='l_info']/h1").InnerText;
            var src = await GetVideoUrl();
            if (src is null)
            {
                Log.Warning("Video has been deleted: {Url}", CurrentUrl);
                return RipInfo.Empty.WithDirectoryName(dirName);
            }
            
            images.Add(src);
        }
        else
        {
            dirName = soup.SelectSingleNode("//h1[@class='c_title']").InnerText;
            var videos = soup.SelectSingleNode("//div[@id='list_videos']")
                             .SelectNodes("./div")
                             .Select(div => div.SelectSingleNode("./a").GetHref());
            foreach (var video in videos)
            {
                var url = $"https://noodlemagazine.com{video}";
                CurrentUrl = url;
                var src = await GetVideoUrl();
                if (src is null)
                {
                    Log.Warning("Video has been deleted: {Url}", url);
                    continue;
                }

                images.Add(src);
            }
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    private async Task<string?> GetVideoUrl()
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
            Log.Debug("Starting video");
            var success =
                await TryClickElementByXPath(
                    "//div[@class='jw-icon jw-icon-display jw-button-color jw-reset']");
            if (!success)
            {
                Log.Debug("Could not find play button, retrying");
                tries.Play++;
                if (tries.Play > 3)
                {
                    Log.Debug("Could not find play button, giving up");
                    tries.Play = 0;
                }
                else
                {
                    continue;
                }
            }

            await Sleep(250);

            Log.Debug("Finding settings button");
            success = await TryClickElementByXPath(
                "//div[@class='jw-icon jw-icon-inline jw-button-color jw-reset jw-icon-settings jw-settings-submenu-button']");
            if (!success)
            {
                Log.Debug("Could not find settings button, retrying");
                continue;
            }

            await Sleep(250);

            Log.Debug("Finding highest quality button");
            success = await TryClickElementByXPath(
                "//div[@id='jw-player_box-settings-submenu-quality']//button");
            if (!success)
            {
                tries.Quality++;
                if (tries.Quality > 3)
                {
                    Log.Debug("Could not find highest quality button, giving up");
                    break;
                }

                Log.Debug("Could not find highest quality button, retrying");
                continue;
            }

            await Sleep(250);

            break;
        }

        var soup = await Soupify();
        var vid = soup.SelectSingleNode("//video[@class='jw-video jw-reset']");
        var src = vid.GetSrc().DecodeUrl();
        if (!src.Contains("rs=") && !src.Contains("url="))
        {
            Log.Warning("Video link does not contain rs=, retrying: {Src}", src);
            Driver.Refresh();
            goto start; // TODO: Figure out a better way to handle this
        }

        return src;
    }

    private async Task<bool> TryClickElementByXPath(string xpath, int msDelay = 250)
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

            Log.Debug("Element '{XPath}' not found, retrying in {msDelay}ms", xpath, msDelay);
            await Task.Delay(msDelay);
        }

        return false;
    }
}