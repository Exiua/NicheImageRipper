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

public class SpankBangParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "spankbang";

    public SpankBangParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                           FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders,
        IHtmlParser.GetFilenameScheme<SpankBangParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for spankbang.com and spankbang.party and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        const string playlistXPath = "//div[contains(concat(' ', @class, ' '), ' video-list ') and " +
                                     "contains(concat(' ', @class, ' '), ' video-rotate ')]";

        if (CurrentUrl.Contains("spankbang.party"))
        {
            Logger.Debug("Switching from spankbang.party to spankbang.com");
            CurrentUrl = CurrentUrl.Replace("spankbang.party", "spankbang.com");
        }

        var ageCheck = Driver.TryFindElement(By.XPath("//button[@id='age-check-yes']"));
        if(ageCheck is not null){
            Driver.Click(ageCheck);
        }
        var soup = await Soupify();
        var blockedHeadline = soup.SelectSingleNode("//h1[@data-translate='block_headline']");
        if (blockedHeadline is not null)
        {
            soup = await SolveParseAddCookies();
        }
        
        string dirName;
        var images = new List<StringImageLinkWrapper>();
        var (urlType, id) = GetUrlType();
        switch (urlType)
        {
            case UrlType.Unknown:
                Logger.Error("Unknown url type: {Url}", CurrentUrl);
                throw new RipperException("Unknown url type");
            case UrlType.Playlist:
            {
                var dirNameNode = soup.SelectSingleNode("//ul[@class='top profile-top']//em") ?? soup.SelectSingleNodeOrThrow("//h1");

                dirName = dirNameNode.InnerText + $" ({id})";
                var videos = soup.SelectSingleNodeOrThrow(playlistXPath)
                                 .SelectNodesOrThrow("./div")
                                 .Select(div => div.SelectSingleNodeOrThrow(".//a").GetHref())
                                 .Select(src => $"https://spankbang.com{src}");
                foreach (var video in videos)
                {
                    Logger.Information("Parsing video: {Video}", video);
                    CurrentUrl = video;
                    var src = await GetVideoUrl();
                    images.Add(src);
                }

                break;
            }
            case UrlType.SingleVideo:
            {
                dirName = soup.SelectSingleNodeOrThrow("//h1[@class='main_content_title']").InnerText;
                var src = await GetVideoUrl();
                images.Add(src);
                break;
            }
            case UrlType.Search:
            {
                dirName = soup.SelectSingleNodeOrThrow("//h1[@class='main_content_title']").InnerText + $" ({id})";
                var videos = soup.SelectSingleNodeOrThrow(playlistXPath)
                                 .SelectNodesOrThrow("./div")
                                 .Select(div => div.SelectSingleNodeOrThrow(".//a").GetHref())
                                 .Select(src => $"https://spankbang.com{src}");
                await File.WriteAllTextAsync("test2.html", Driver.PageSource);
                foreach (var video in videos)
                {
                    CurrentUrl = video;
                    var src = await GetVideoUrl();
                    images.Add(src);
                }

                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    private async Task<string> GetVideoUrl()
    {
        while (true)
        {
            var playButton = Driver.TryFindElement(By.XPath("//button[@class='vjs-big-play-button']"));
            //playButton?.Click();
            if (playButton is not null)
            {
                Driver.Click(playButton);
                break;
            }
            
            var playButton2 = Driver.TryFindElement(By.XPath("//button[@class='vjs-play-control vjs-control vjs-button']"));
            if (playButton2 is not null)
            {
                Driver.Click(playButton2);
                break;
            }
            
            var playButton3 = Driver.TryFindElement(By.XPath("//button[@class='vjs-play-control vjs-control vjs-button vjs-paused']"));
            if (playButton3 is not null)
            {
                Driver.Click(playButton3);
                break;
            }
            
            var playButton4 = Driver.TryFindElement(By.XPath("//button[@class='vjs-play-control vjs-control vjs-button vjs-playing']"));
            if (playButton4 is not null)
            {
                break;
            }

            await Sleep(250);
        }

        var settingsButton =
            Driver.FindElement(By.XPath("//button[@class='vjs-control vjs-button vjs-settings-button']"));
        //settingsButton.Click();
        Driver.Click(settingsButton);
        var qualityButton = Driver.FindElement(By.XPath(
            "//button[@class='!flex max-h-11 items-center justify-between !border-x-0 !border-b !border-t-0 !border-solid !border-surface-quaternary py-3 pl-4 pr-5 md:max-h-8 md:py-2']"));
        //qualityButton.Click();
        Driver.Click(qualityButton);
        var highestQualityButton =
            Driver.FindElement(By.XPath("//button[@class='quality-item submenu-item'][2]"));
        //highestQualityButton.Click();
        Driver.Click(highestQualityButton);
        var soup = await Soupify();
        var src = soup.SelectSingleNodeOrThrow("//video[@id='main_video_player_html5_api']").GetSrc().DecodeUrl();
        return src;
    }

    private (UrlType, string) GetUrlType()
    {
        var id = CurrentUrl.Split("/")[3];
        if (CurrentUrl.Contains("/playlist/"))
        {
            return id.Contains('-') ? (UrlType.SingleVideo, id) : (UrlType.Playlist, id);
        }

        if (CurrentUrl.Contains("/video/"))
        {
            return (UrlType.SingleVideo, id);
        }

        if (CurrentUrl.Contains("/s/"))
        {
            return (UrlType.Search, id);
        }

        return (UrlType.Unknown, id);
    }

    private enum UrlType
    {
        Unknown,
        Playlist,
        SingleVideo,
        Search,
    }
}