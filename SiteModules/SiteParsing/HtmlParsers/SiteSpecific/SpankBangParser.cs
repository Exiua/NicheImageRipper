using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.SiteParsing.HtmlParsers;
using OpenQA.Selenium;
using HtmlAgilityPack;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class SpankBangParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "spankbang";
    public static string[] SupportedUrls => ["https://spankbang.com/", "https://spankbang.party/"];

    public SpankBangParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<SpankBangParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for spankbang.com and spankbang.party and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        const string playlistXPath = "//div[contains(concat(' ', @class, ' '), ' video-list ') and " + "contains(concat(' ', @class, ' '), ' video-rotate ')]";
        if (CurrentUrl.Contains("spankbang.party"))
        {
            Logger.Debug("Switching from spankbang.party to spankbang.com");
            CurrentUrl = CurrentUrl.Replace("spankbang.party", "spankbang.com");
        }

        var ageCheck = Driver.TryFindElement(By.XPath("//button[@id='age-check-yes']"));
        if (ageCheck is not null)
        {
            Driver.Click(ageCheck);
        }

        var soup = await Soupify(cancellationToken: cancellationToken);
        var blockedHeadline = soup.SelectSingleNode("//h1[@data-translate='block_headline']");
        if (blockedHeadline is not null)
        {
            soup = await SolveParseAddCookies(cancellationToken: cancellationToken);
        }

        string dirName;
        var images = new List<StringFileLinkWrapper>();
        var(urlType, id) = GetUrlType();
        switch (urlType)
        {
            case UrlType.Unknown:
                Logger.Error("Unknown url type: {Url}", CurrentUrl);
                throw new RipperException("Unknown url type");
            case UrlType.Playlist:
            {
                var dirNameNode = soup.SelectSingleNode("//ul[@class='top profile-top']//em") ?? soup.SelectSingleNodeOrThrow("//h1");
                dirName = dirNameNode.InnerText + $" ({id})";
                var videos = soup.SelectSingleNodeOrThrow(playlistXPath).SelectNodesOrThrow("./div").Select(div => div.SelectSingleNodeOrThrow(".//a").GetHref()).Select(src => $"https://spankbang.com{src}");
                foreach (var video in videos)
                {
                    Logger.Information("Parsing video: {Video}", video);
                    CurrentUrl = video;
                    var src = await GetVideoUrl(cancellationToken: cancellationToken);
                    images.Add(src);
                }

                break;
            }

            case UrlType.SingleVideo:
            {
                dirName = soup.SelectSingleNodeOrThrow("//h1[@class='main_content_title']").InnerText;
                var src = await GetVideoUrl(cancellationToken: cancellationToken);
                images.Add(src);
                break;
            }

            case UrlType.Search:
            {
                dirName = soup.SelectSingleNodeOrThrow("//h1[@class='main_content_title']").InnerText + $" ({id})";
                var videos = soup.SelectSingleNodeOrThrow(playlistXPath).SelectNodesOrThrow("./div").Select(div => div.SelectSingleNodeOrThrow(".//a").GetHref()).Select(src => $"https://spankbang.com{src}");
                await File.WriteAllTextAsync("test2.html", Driver.PageSource, cancellationToken: cancellationToken);
                foreach (var video in videos)
                {
                    CurrentUrl = video;
                    var src = await GetVideoUrl(cancellationToken: cancellationToken);
                    images.Add(src);
                }

                break;
            }

            default:
                throw new ArgumentOutOfRangeException();
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    private async Task<string> GetVideoUrl(CancellationToken cancellationToken = default)
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

            await Sleep(250, cancellationToken: cancellationToken);
        }

        var settingsButton = Driver.FindElement(By.XPath("//button[@class='vjs-control vjs-button vjs-settings-button']"));
        //settingsButton.Click();
        Driver.Click(settingsButton);
        var qualityButton = Driver.FindElement(By.XPath("//button[@class='!flex max-h-11 items-center justify-between !border-x-0 !border-b !border-t-0 !border-solid !border-surface-quaternary py-3 pl-4 pr-5 md:max-h-8 md:py-2']"));
        //qualityButton.Click();
        Driver.Click(qualityButton);
        var highestQualityButton = Driver.FindElement(By.XPath("//button[@class='quality-item submenu-item'][2]"));
        //highestQualityButton.Click();
        Driver.Click(highestQualityButton);
        var soup = await Soupify(cancellationToken: cancellationToken);
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