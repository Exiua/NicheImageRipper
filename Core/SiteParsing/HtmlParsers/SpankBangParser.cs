using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class SpankBangParser : HtmlParser
{
    public SpankBangParser(WebDriver driver, Dictionary<string, string> requestHeaders,
                                 FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for spankbang.com and spankbang.party and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        const string playlistXPath = "//div[contains(concat(' ', @class, ' '), ' video-list ') and " +
                                     "contains(concat(' ', @class, ' '), ' video-rotate ') and " +
                                     "contains(concat(' ', @class, ' '), ' four-col ')]";

        if (CurrentUrl.Contains("spankbang.party"))
        {
            Log.Debug("Switching from spankbang.party to spankbang.com");
            CurrentUrl = CurrentUrl.Replace("spankbang.party", "spankbang.com");
        }

        var ageCheck = Driver.TryFindElement(By.XPath("//button[@id='age-check-yes']"));
        ageCheck?.Click();
        var soup = await Soupify();
        string dirName;
        var images = new List<StringImageLinkWrapper>();
        var (urlType, id) = GetUrlType();
        switch (urlType)
        {
            case UrlType.Unknown:
                Log.Error("Unknown url type: {Url}", CurrentUrl);
                throw new RipperException("Unknown url type");
            case UrlType.Playlist:{
                dirName = soup.SelectSingleNode("//ul[@class='top profile-top']//em").InnerText + $" ({id})";
                var videos = soup.SelectSingleNode(playlistXPath)
                                 .SelectNodes("./div")
                                 .Select(div => div.SelectSingleNode(".//a").GetHref())
                                 .Select(src => $"https://spankbang.com{src}");
                foreach (var video in videos)
                {
                    CurrentUrl = video;
                    var src = await GetVideoUrl();
                    images.Add(src);
                }
                break;
            }
            case UrlType.SingleVideo:
            {
                dirName = soup.SelectSingleNode("//h1[@class='main_content_title']").InnerText;
                var src = await GetVideoUrl();
                images.Add(src);
                break;
            }
            case UrlType.Search:
            {
                dirName = soup.SelectSingleNode("//h1[@class='main_content_title']").InnerText + $" ({id})";
                var videos = soup.SelectSingleNode(playlistXPath)
                                 .SelectNodes("./div")
                                 .Select(div => div.SelectSingleNode(".//a").GetHref())
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
        var playButton = Driver.TryFindElement(By.XPath("//button[@class='vjs-big-play-button']"));
        //playButton?.Click();
        if (playButton is not null)
        {
            Driver.Click(playButton);
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
        var src = soup.SelectSingleNode("//video[@id='main_video_player_html5_api']").GetSrc().DecodeUrl();
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