﻿using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Core.Configuration;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.Utility;
using HtmlAgilityPack;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using Cookie = OpenQA.Selenium.Cookie;

namespace Core.SiteParsing;

public partial class HtmlParser
{
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:108.0) Gecko/20100101 Firefox/108.0";

    private const string Protocol = "https:";

    private static readonly string[] ExternalSites =
        ["drive.google.com", "mega.nz", "mediafire.com", "sendvid.com", "dropbox.com"];

    private static readonly string[] ParsableSites = ["drive.google.com", "mega.nz", "sendvid.com", "dropbox.com"];

    private static bool LoggedIn;

    private static FirefoxDriver Driver { get; set; } = new(InitializeOptions(""));
    private static Dictionary<string, bool> SiteLoginStatus { get; set; } = new();

    public bool Interrupted { get; set; }
    private string SiteName { get; set; }
    public float SleepTime { get; set; }
    public float Jitter { get; set; }
    private string GivenUrl { get; set; }
    private FilenameScheme FilenameScheme { get; set; }
    private Dictionary<string, string> RequestHeaders { get; set; }

    private static string CurrentUrl
    {
        get => Driver.Url;
        set => Driver.Url = value;
    }

    public HtmlParser(Dictionary<string, string> requestHeaders, string siteName = "",
                      FilenameScheme filenameScheme = FilenameScheme.Original)
    {
        //var options = InitializeOptions(siteName);
        //Driver = new FirefoxDriver(options);
        RequestHeaders = requestHeaders;
        FilenameScheme = filenameScheme;
        Interrupted = false;
        SiteName = siteName;
        SleepTime = 0.2f;
        Jitter = 0.5f;
        GivenUrl = "";
    }

    public async Task<RipInfo> ParseSite(string url)
    {
        if (File.Exists("partial.json"))
        {
            var saveData = ReadPartialSave();
            if (saveData.TryGetValue(url, out var value))
            {
                RequestHeaders["cookie"] = value.Cookies;
                RequestHeaders["referer"] = value.Referer;
                Interrupted = true;
                return value.RipInfo;
            }
        }

        url = url.Replace("members.", "www.");
        GivenUrl = url;
        CurrentUrl = url;
        (SiteName, SleepTime) = UrlUtility.SiteCheck(GivenUrl, RequestHeaders);
        var siteParser = GetParser(SiteName);
        try
        {
            var siteInfo = await siteParser();
            WritePartialSave(siteInfo, url);
            //pickle.dump(self.driver.get_cookies(), open("cookies.pkl", "wb"))
            return siteInfo;
        }
        catch
        {
            Print(CurrentUrl);
            throw;
        }
    }

    private Func<Task<RipInfo>> GetParser(string siteName)
    {
        return siteName switch
        {
            "imhentai" => ImhentaiParse,
            "kemono" => KemonoParse,
            "coomer" => CoomerParse,
            "sankakucomplex" => SankakuComplexParse,
            "omegascans" => OmegaScansParse,
            "redgifs" => RedGifsParse,
            "rule34" => Rule34Parse,
            "gelbooru" => GelbooruParse,
            "danbooru" => DanbooruParse,
            "google" => GoogleParse,
            "dropbox" => DropboxParse,
            "imgur" => ImgurParse,
            "newgrounds" => NewgroundsParse,
            "wnacg" => WnacgParse,
            "arca" => ArcaParse,
            "babecentrum" => BabeCentrumParse,
            "babeimpact" => BabeImpactParse,
            "babeuniversum" => BabeUniversumParse,
            "babesandbitches" => BabesAndBitchesParse,
            "babesandgirls" => BabesAndGirlsParse,
            "babesaround" => BabesAroundParse,
            "babesbang" => BabesBangParse,
            "babesinporn" => BabesInPornParse,
            "babesmachine" => BabesMachineParse,
            "bestprettygirl" => BestPrettyGirlParse,
            "bitchesgirls" => BitchesGirlsParse,
            "bunkr" => BunkrParse,
            "buondua" => BuonduaParse,
            "bustybloom" => BustyBloomParse,
            "camwhores" => CamwhoresParse,
            "cherrynudes" => CherryNudesParse,
            "chickteases" => ChickTeasesParse,
            "cool18" => Cool18Parse,
            "cutegirlporn" => CuteGirlPornParse,
            "cyberdrop" => CyberDropParse,
            "decorativemodels" => DecorativeModelsParse,
            //DeviantArt
            "dirtyyoungbitches" => DirtyYoungBitchesParse,
            "eahentai" => EahentaiParse,
            "8boobs" => EightBoobsParse,
            "8muses" => EightMusesParse,
            "elitebabes" => EliteBabesParse,
            "erosberry" => ErosBerryParse,
            "erohive" => EroHiveParse,
            "erome" => EroMeParse,
            "erothots" => EroThotsParse,
            "everia" => EveriaParse,
            "exgirlfriendmarket" => ExGirlFriendMarketParse,
            "fapello" => FapelloParse,
            "faponic" => FaponicParse,
            "f5girls" => F5GirlsParse,
            "femjoyhunter" => FemJoyHunterParse,
            "flickr" => FlickrParse,
            "foxhq" => FoxHqParse,
            "ftvhunter" => FtvHunterParse,
            "ggoorr" => GgoorrParse,
            "girlsofdesire" => GirlsOfDesireParse,
            "girlsreleased" => GirlsReleasedParse,
            "glam0ur" => Glam0urParse,
            "grabpussy" => GrabPussyParse,
            "gyrls" => GyrlsParse,
            "hegrehunter" => HegreHunterParse,
            "hentai-cosplays" => HentaiCosplaysParse,
            "hentairox" => HentaiRoxParse,
            "hustlebootytemptats" => HustleBootyTempTatsParse,
            "hotgirl" => HotGirlParse,
            "hotstunners" => HotStunnersParse,
            "hottystop" => HottyStopParse,
            "100bucksbabes" => HundredBucksBabesParse,
            "imgbox" => ImgBoxParse,
            "influencersgonewild" => InfluencersGoneWildParse,
            "inven" => InvenParse,
            "jkforum" => JkForumParse,
            "join2babes" => Join2BabesParse,
            "joymiihub" => JoyMiiHubParse,
            "leakedbb" => LeakedBbParse,
            "livejasminbabes" => LiveJasminBabesParse,
            "luscious" => LusciousParse,
            "mainbabes" => MainBabesParse,
            "manganato" or "chapmanganato" => ManganatoParse,
            "metarthunter" => MetArtHunterParse,
            "morazzia" => MorazziaParse,
            "myhentaigallery" => MyHentaiGalleryParse,
            "micmicdoll" => MicMicDollParse,
            "nakedgirls" => NakedGirlsParse,
            "nhentai" => NHentaiParse,
            "nightdreambabe" => NightDreamBabeParse,
            "nijie" => NijieParse,
            "animeh" => AnimehParse,
            "novoglam" => NovoGlamParse,
            "novohot" => NovoHotParse,
            "novoporn" => NovoPornParse,
            "nudebird" => NudeBirdParse,
            "nudity911" => Nudity911Parse,
            "pbabes" => PBabesParse,
            "pixeldrain" => PixelDrainParse,
            "pmatehunter" => PMateHunterParse,
            "porn3dx" => Porn3dxParse,
            "pornhub" => PornhubParse,
            "putmega" => PutMegaParse,
            "rabbitsfun" => RabbitsFunParse,
            "redpornblog" => RedPornBlogParse,
            "rossoporn" => RossoPornParse,
            "sensualgirls" => SensualGirlsParse,
            "sexhd" => SexHdParse,
            "sexyaporno" => SexyAPornoParse,
            "sexybabesart" => SexyBabesArtParse,
            "sexykittenporn" => SexyKittenPornParse,
            "sexynakeds" => SexyNakedsParse,
            "sfmcompile" => SfmCompileParse,
            "silkengirl" => SilkenGirlParse,
            "simply-cosplay" => SimplyCosplayParse,
            "sxchinesegirlz01" => SxChineseGirlz01Parse,
            "pleasuregirl" => PleasureGirlParse,
            "theomegaproject" => TheOmegaProjectParse,
            _ => throw new Exception($"Site not supported/implemented: {siteName}")
        };
    }

    private static Dictionary<string, PartialSaveEntry> ReadPartialSave()
    {
        return JsonUtility.Deserialize<Dictionary<string, PartialSaveEntry>>("partial.json")!;
    }

    private void WritePartialSave(RipInfo ripInfo, string url)
    {
        var partialSaveEntry = new PartialSaveEntry
        {
            Cookies = RequestHeaders["cookie"],
            Referer = RequestHeaders["referer"],
            RipInfo = ripInfo
        };
        var partialSave = new Dictionary<string, PartialSaveEntry> { { url, partialSaveEntry } };
        JsonUtility.Serialize("partial.json", partialSave);
    }

    private static FirefoxOptions InitializeOptions(string siteName)
    {
        var options = new FirefoxOptions();
        if ((siteName != "v2ph" || LoggedIn) && siteName != "debug")
        {
            options.AddArgument("--headless");
        }

        options.SetPreference("general.useragent.override", UserAgent);
        options.SetPreference("media.volume_scale", "0.0");
        
        return options;
    }

    private Task<bool> SiteLogin()
    {
        if (IsLoggedInToSite(SiteName))
        {
            return Task.FromResult(true);
        }

        var loginTask = SiteName switch
        {
            "nijie" => NijieLogin(),
            _ => throw new Exception("Site authentication not implemented")
        };
        
        return loginTask.ContinueWith(task =>
        {
            SiteLoginStatus[SiteName] = task.Result;
            return task.Result;
        });
    }

    private static bool IsLoggedInToSite(string siteName)
    {
        return !SiteLoginStatus.TryAdd(siteName, false) && SiteLoginStatus[siteName];
    }

    private static async Task<bool> NijieLogin()
    {
        var origUrl = CurrentUrl;
        var (username, password) = Config.Instance.Logins["Nijie"];
        CurrentUrl = "https://nijie.info/login.php";
        if (CurrentUrl.Contains("age_ver.php"))
        {
            Driver.FindElement(By.XPath("//li[@class='ok']")).Click();
            while (!CurrentUrl.Contains("login.php"))
            {
                await Task.Delay(100);
            }
        }
        
        Driver.FindElement(By.XPath("//input[@name='email']")).SendKeys(username);
        Driver.FindElement(By.XPath("//input[@name='password']")).SendKeys(password);
        Driver.FindElement(By.XPath("//input[@class='login_button']")).Click();
        while (CurrentUrl.Contains("login.php"))
        {
            await Task.Delay(100);
        }
        
        CurrentUrl = origUrl;
        return true;
    }

    #region Site Parsers

    #region Generic Site Parsers

    /// <summary>
    ///     Parses the html for kemono.su and coomer.su and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <param name="domainUrl">The domain url of the site</param>
    /// <returns></returns>
    private async Task<RipInfo> DotPartyParse(string domainUrl)
    {
        var cookies = Driver.Manage().Cookies.AllCookies;
        var cookieStr = cookies.Aggregate("", (current, c) => current + $"{c.Name}={c.Value};");
        RequestHeaders["cookie"] = cookieStr;
        var baseUrl = CurrentUrl;
        var urlSplit = baseUrl.Split("/");
        var sourceSite = urlSplit[3];
        baseUrl = string.Join("/", urlSplit[..6]).Split("?")[0];
        var pageUrl = CurrentUrl.Split("?")[0];
        await Task.Delay(5000);
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@id='user-header__info-top']")
                          .SelectSingleNode("//span[@itemprop='name']").InnerText;
        dirName = $"{dirName} - ({sourceSite})";

        #region Get All Posts

        var page = 0;
        var imageLinks = new List<string>();
        var origUrl = CurrentUrl;
        while (true)
        {
            page += 1;
            Print($"Parsing page {page}");
            var imageList = soup.SelectSingleNode("//div[@class='card-list__items']").SelectNodes("//article");
            foreach (var image in imageList)
            {
                var id = image.GetAttributeValue("data-id", "");
                imageLinks.Add($"{baseUrl}/post/{id}");
            }

            var nextUrl = $"{pageUrl}?o={page * 50}";
            Print(nextUrl);
            soup = await Soupify(nextUrl);
            if (soup.SelectSingleNode("//h2[@class='site-section__subheading']") is not null || CurrentUrl == origUrl)
            {
                break;
            }
        }

        #endregion

        #region Parse All Posts

        var images = new List<string>();
        var externalLinks = CreateExternalLinkDict();
        var numPosts = imageLinks.Count;
        string[] attachmentExtensions =
            [".zip", ".rar", ".mp4", ".webm", ".psd", ".clip", ".m4v", ".7z", ".jpg", ".png", ".webp"];

        foreach (var (i, link) in imageLinks.Enumerate())
        {
            Print($"Parsing post {i + 1} of {numPosts}");
            Print(link);
            soup = await Soupify(link);
            var links = soup.SelectNodes("//a").GetHrefs();
            var possibleLinksP = soup.SelectNodes("//p");
            var possibleLinks = possibleLinksP.Select(p => p.InnerText).ToList();
            var possibleLinksDiv = soup.SelectNodes("//div");
            possibleLinks.AddRange(possibleLinksDiv.Select(d => d.InnerText));
            var extLinks = ExtractExternalUrls(links);
            foreach (var site in extLinks.Keys)
            {
                externalLinks[site].AddRange(extLinks[site]);
            }

            extLinks = ExtractPossibleExternalUrls(possibleLinks);
            foreach (var site in extLinks.Keys)
            {
                externalLinks[site].AddRange(extLinks[site]);
            }

            var attachments = new List<string>();
            foreach (var l in links)
            {
                if (!attachmentExtensions.AnyIn(l))
                {
                    continue;
                }

                var attachment = l.Contains(domainUrl) || l.Contains("http") /* Includes https */ ? l : domainUrl + l;
                attachments.Add(attachment);
            }

            images.AddRange(attachments);
            foreach (var site in ParsableSites)
            {
                images.AddRange(externalLinks[site]);
            }

            var imageListContainer = soup.SelectSingleNode("//div[@class='post__files']");
            if (imageListContainer is null)
            {
                continue;
            }

            var imageList = imageListContainer.SelectNodes("//a[@class='fileThumb image-link']");
            var imageListLinks = imageList.GetHrefs();
            images.AddRange(imageListLinks);
        }

        #endregion

        foreach (var site in ExternalSites)
        {
            externalLinks[site] = externalLinks[site].RemoveDuplicates();
        }

        SaveExternalLinks(externalLinks);
        images = images.RemoveDuplicates();
        var oldLinks = images;
        var stringLinks = new List<StringImageLinkWrapper>();
        foreach (var link in oldLinks)
        {
            if (!link.Contains("dropbox.com/"))
            {
                stringLinks.Add(link);
            }
            else
            {
                var ripInfo = await DropboxParse(link);
                stringLinks.AddRange(ripInfo.Urls.Select(x => new StringImageLinkWrapper(x)));
            }
        }

        // Remove duplicates
        var seen = new HashSet<string>();
        var unique = new List<StringImageLinkWrapper>();
        foreach (var link in stringLinks)
        {
            var domainName = new Uri(link).Host.Split('.')[0];
            var linkStr = link.ToString();
            if (!linkStr.Contains(domainName)) // Only native links get duplicated
            {
                unique.Add(link);
                continue;
            }

            var fileName = linkStr.Split("/")[^1];
            if (seen.Add(fileName))
            {
                unique.Add(link);
            }
        }

        return new RipInfo(unique, dirName, FilenameScheme);
    }

    /// <summary>
    ///     
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> GenericBabesHtmlParser(string dirNameXpath, string imageContainerXpath)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode(dirNameXpath)
                          .InnerText;
        var images = soup.SelectNodes(imageContainerXpath)
                         .SelectMany(im => im.SelectNodes(".//img"))
                         .Select(img => Protocol + img.GetSrc().Remove("tn_"))
                         .Select(dummy => (StringImageLinkWrapper)dummy)
                         .ToList();
        
        return new RipInfo(images, dirName, FilenameScheme);
    }

    private Task<RipInfo> GenericHtmlParser(string siteName)
    {
        return siteName switch
        {
            "bustybloom" or "sexyaporno" => GenericHtmlParserHelper1(), // Formerly 2
            "elitebabes" => GenericHtmlParserHelper2(),
            "femjoyhunter" or "ftvhunter" or "hegrehunter" or "joymiihub" 
                or "metarthunter" or "pmatehunter" => GenericHtmlParserHelper3(),
            _ => throw new RipperException($"Invalid site name: {siteName}")
        };
    }
    
    /// <summary>
    ///     
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> GenericHtmlParserHelper1()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//img[@title='Click To Enlarge!']")
                          .GetAttributeValue("alt")
                          .Split(" ")
                          .TakeWhile(s => s != "-")
                          .Join(" ");
        var images = soup.SelectNodes("//div[@class='gallery_thumb']")
                            .Select(img => Protocol + img.SelectSingleNode(".//img").GetSrc().Remove("tn_"))
                            .ToStringImageLinkWrapperList();

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> GenericHtmlParserHelper2()
    {
        var soup = await Soupify();
        var imageList = soup.SelectSingleNode("//ul[@class='list-gallery static css has-data']")
                            .SelectNodes(".//a");
        var images = imageList.Select(image => image.GetHref())
                              .Select(dummy => (StringImageLinkWrapper)dummy)
                              .ToList();
        var dirName = imageList[0].SelectSingleNode(".//img")
                                 .GetAttributeValue("alt");

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> GenericHtmlParserHelper3()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//header[@id='top']").SelectSingleNode(".//h1").InnerText;
        var images = soup.SelectSingleNode("//ul[contains(@class, 'list-gallery') and contains(@class, 'static') and contains(@class, 'css')]")
                         .SelectNodes(".//a")
                         .Select(img => img.GetHref())
                         .Select(dummy => (StringImageLinkWrapper)dummy)
                         .ToList();

        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    #endregion

    // TODO: Test that method works or remove if the site is permanently down
    /// <summary>
    ///     Parses the html for agirlpic.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> AGirlPicParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='entry-title'").InnerText;
        var baseUrl = CurrentUrl;
        var numPages = soup.SelectSingleNode("//div[@class='page-links']")
                          .SelectNodes("./a")
                          .Count + 1;
        var images = new List<StringImageLinkWrapper>();
        for (var i = 1; i <= numPages; i++)
        {
            var tags = soup.SelectSingleNode("//div[@class='entry-content clear']")
                           .SelectNodes("./div[@class='separator']")
                           .ToList();
            foreach (var imgTags in tags.Select(tag => tag.SelectNodes(".//img")))
            {
                images.AddRange(imgTags.Select(img => img.GetSrc()).Select(dummy => (StringImageLinkWrapper)dummy));
            }

            if (i != numPages)
            {
                soup = await Soupify($"{baseUrl}{i + 1}/");
            }
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for animeh.to and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> AnimehParse()
    {
        string dirName;
        List<StringImageLinkWrapper> images;
        if(CurrentUrl.Contains("/hchapter/"))
        {
            CurrentUrl = CurrentUrl.Split("?")[0] + "?tab=reading";
            var soup = await Soupify();
            dirName = soup.SelectSingleNode("//h1[@class='main-night container']").InnerText
                              .Split("Manga")[1]
                              .Trim();
            var metadata = soup.SelectSingleNode("//div[@class='col-md-8 col-sm-12']");
            var pageCountRaw = metadata.SelectNodes("./div")[2].InnerText.Split(" ")[1];
            var pageCount = int.Parse(pageCountRaw);
            var imageUrl = soup.SelectSingleNode("//div[@id='pictureViewer']")
                               .SelectSingleNode(".//img")
                               .GetSrc();
            var urlParts = imageUrl.Split("/");
            imageUrl = "/".Join(urlParts[..^1]);
            var extension = urlParts[^1].Split(".")[1];
            images = [];
            for (var i = 1; i <= pageCount; i++)
            {
                var image = $"{imageUrl}/{i}.{extension}";
                images.Add(image);
            }
        }
        else if (CurrentUrl.Contains("/hepisode/"))
        {
            // var highestQualityButton = Driver.FindElement(By.XPath("(//div[@class='source-btn-group']/a)[2]"));
            // highestQualityButton.Click();
            var soup = await Soupify();
            dirName = soup.SelectSingleNode("//h1[@class='main-night']").InnerText
                              .Split("Hentai")[1]
                              .Trim();
            var playButton = Driver.FindElement(By.XPath("//button[@class='btn btn-play']"));
            playButton.Click();
            await WaitForElement("//iframe[@id='episode-frame']", timeout: -1);
            var iframe = Driver.FindElement(By.XPath("//iframe[@id='episode-frame']"));
            Driver.SwitchTo().Frame(iframe);
            await WaitForElement("//video/source", timeout: -1);
            var videoSource = Driver.FindElement(By.XPath("//video/source")).GetAttribute("src");
            images = [videoSource];
        }
        else // Assume ASMR
        {
            var soup = await Soupify();
            dirName = soup.SelectSingleNode("//h1[@class='main-night']").InnerText
                          .Split(" ")[2..]
                          .Join(" ");
            images = [];
            var tracks =
                Driver.FindElements(By.XPath("//*[@id='__layout']/div/div[3]/main/div[1]/div/div[1]/div[1]/div[2]/a"));
            foreach (var track in tracks)
            {
                await Task.Delay(100);
                track.Click();
                var trackSource = Driver.FindElement(By.XPath("//audio")).GetAttribute("src");
                images.Add(trackSource);
            }

            var thumbnails = Driver.FindElement(By.XPath("//div[@class='d-flex image-board mb image-board-asmr']"))
                                   .FindElements(By.XPath("./div[@class='d-flex flex-column']"));
            foreach (var thumbnail in thumbnails)
            {
                thumbnail.Click();
                var thumbnailSource = Driver.FindElement(By.XPath("//div[@class='vgs__container']//img"))
                                            .GetAttribute("src");
                images.Add(thumbnailSource);
                var closeButton = Driver.FindElement(By.XPath("//button[@class='btn btn-danger vgs__close']"));
                closeButton.Click();
                await Task.Delay(500);
            }
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    /// <summary>
    ///     Parses the html for arca.live and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> ArcaParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='title']").InnerText;
        var mainTag = soup.SelectSingleNode("//div[@class='fr-view article-content']");

        var images = new List<StringImageLinkWrapper>();
        var imgNodes = mainTag.SelectNodes(".//img");
        if(imgNodes is not null)
        {
            var imageList = mainTag.SelectNodes(".//img").GetSrcs();
            var imgs = imageList
                        .Select(image => image.Split("?")[0] + "?type=orig") // Remove query string and add type=orig
                        .Select(img => !img.Contains(Protocol) ? Protocol + img : img) // Add protocol if missing
                        .Select(dummy => (StringImageLinkWrapper)dummy) // Convert to StringImageLinkWrapper
                        .ToList();
            images.AddRange(imgs);
        }
        
        var videoNodes = mainTag.SelectNodes(".//video");
        if(videoNodes is not null)
        {
            var videoList = mainTag.SelectNodes(".//video").GetSrcs();
            var videos = videoList
                        .Select(video => !video.Contains(Protocol) ? Protocol + video : video)
                        .Select(dummy => (StringImageLinkWrapper)dummy)
                        .ToList();
            images.AddRange(videos);
        }
        
        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    /// <summary>
    ///     Parses the html for artstation.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> ArtstationParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='artist-name']").InnerText;
        var username = CurrentUrl.Split("/")[3];
        var cacheScript = soup.SelectSingleNode("//div[@class='wrapper-main']").SelectNodes(".//script")[1].InnerText;
        
        #region Id Extraction
        
        var start = cacheScript.IndexOf("quick.json", StringComparison.Ordinal);
        var end = cacheScript.LastIndexOf(");", StringComparison.Ordinal);
        var jsonData = cacheScript[(start + 14)..(end - 1)].Replace("\n", "").Replace('\"', '"');
        var json = JsonSerializer.Deserialize<JsonNode>(jsonData);
        var userId = json!["id"]!.Deserialize<string>()!;
        var userName = json["full_name"]!.Deserialize<string>()!;
        
        #endregion
        
        #region Get Posts
        
        var total = 1;
        var pageCount = 1;
        var firstIter = true;
        var posts = new List<string>();
        var client = new HttpClient();

        while (total > 0)
        {
            Console.WriteLine(pageCount);
            var url = $"https://www.artstation.com/users/{username}/projects.json?page={pageCount}";
            Console.WriteLine(url);
            var response = await client.GetAsync(url);
            var responseData = await response.Content.ReadFromJsonAsync<JsonNode>();
            var data = responseData!["data"]!.AsArray();
            foreach (var d in data)
            {
                var link = d!["permalink"]!.Deserialize<string>()!.Split("/")[4];
                posts.Add(link);
            }
            
            if (firstIter)
            {
                total = responseData["total_count"]!.Deserialize<int>() - data.Count;
                firstIter = false;
            }
            else
            {
                total -= data.Count;
            }
            
            pageCount += 1;
            await Task.Delay(100);
        }
        
        #endregion
        
        #region Get Media Links
        
        var images = new List<StringImageLinkWrapper>();
        foreach (var post in posts)
        {
            var url = $"https://www.artstation.com/projects/{post}.json";
            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(url);
            }
            catch (HttpRequestException)
            {
                await Task.Delay(5000);
                response = await client.GetAsync(url);
            }
            
            var responseData = await response.Content.ReadFromJsonAsync<JsonNode>();
            var assets = responseData!["assets"]!.AsArray();
            var urls = assets.Select(asset => asset!["image_url"]!.Deserialize<string>()!.Replace("/large/", "/4k/"));
            images.AddRange(urls.Select(url => (StringImageLinkWrapper)url));
        }
        
        #endregion
        
        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for babecentrum.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> BabeCentrumParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='pageHeading']")
                          .SelectNodes(".//cufontext")
                          .Select(w => w.InnerText)
                          .Join(" ")
                          .Trim();
        var images = soup.SelectSingleNode("//table")
                            .SelectNodes(".//img")
                            .Select(img => Protocol + img.GetAttributeValue("src", "").Remove("tn_"))
                            .Select(dummy => (StringImageLinkWrapper)dummy)   
                            .ToList();

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for babeimpact.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> BabeImpactParse()
    {
        var soup = await Soupify();
        var title = soup.SelectSingleNode("//h1[@class='blockheader pink center lowercase']").InnerText;
        var sponsor = soup.SelectSingleNode("//div[@class='c']")
                         .SelectNodes(".//a")[1]
                         .InnerText
                         .Trim();
        sponsor = $"({sponsor})";
        var dirName = $"{sponsor} {title}";
        var tags = soup.SelectNodes("//div[@class='list gallery']");
        var tagList = new List<HtmlNode>();
        foreach (var tag in tags)
        {
            tagList.AddRange(tag.SelectNodes(".//div[@class='item']"));
        }

        var images = new List<StringImageLinkWrapper>();
        var imageList = tagList.Select(tag => tag.SelectSingleNode(".//a")).Select(anchor => $"https://babeimpact.com{anchor.GetHref()}").ToList();
        foreach (var image in imageList)
        {
            soup = await Soupify(image);
            var img = soup.SelectSingleNode("//div[@class='image-wrapper']")
                         .SelectSingleNode(".//img")
                         .GetSrc();
            images.Add((StringImageLinkWrapper)(Protocol + img));
        }
        
        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for babeuniversum.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> BabeUniversumParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='title']")
                          .SelectSingleNode(".//h1")
                          .InnerText;
        var images = soup.SelectSingleNode("//div[@class='three-column']")
                         .SelectNodes(".//div[@class='thumbnail']")
                         .Select(img => Protocol + img.SelectSingleNode(".//img").GetSrc().Remove("tn_"))
                         .Select(dummy => (StringImageLinkWrapper)dummy)
                         .ToList();

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for babesandbitches.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> BabesAndBitchesParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@id='title']")
                          .InnerText
                          .Split("picture")[0]
                          .Trim();
        var images = soup.SelectNodes("//a[@class='gallery-thumb']")
                            .Select(img => Protocol + img.SelectSingleNode(".//img").GetSrc().Remove("tn_"))
                            .ToStringImageLinkWrapperList();

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for babesandgirls.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> BabesAndGirlsParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='title']")
                          .InnerText;
        var images = soup.SelectSingleNode("//div[@class='block-post album-item']")
                            .SelectNodes(".//a[@class='item-post']")
                            .Select(img => Protocol + img.SelectSingleNode(".//img").GetSrc().Remove("tn_"))
                            .ToStringImageLinkWrapperList();

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for babesaround.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> BabesAroundParse()
    {
        return GenericBabesHtmlParser("//section[@class='outer-section']//h2", "//div[@class='lightgallery thumbs quadruple fivefold']");
    }

    /// <summary>
    ///     Parses the html for babesbang.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> BabesBangParse()
    {
        return GenericBabesHtmlParser("//div[@class='main-title']", "//div[@class='gal-block']");
    }

    /// <summary>
    ///     Parses the html for babesinporn.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> BabesInPornParse()
    {
        return GenericBabesHtmlParser("//h1[@class='blockheader pink center lowercase']", "//div[@class='list gallery']");
    }

    /// <summary>
    ///     Parses the html for babesmachine.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> BabesMachineParse()
    {
        var soup = await Soupify();
        var gallery = soup.SelectSingleNode("//div[@id='gallery']");
        var dirName = gallery.SelectSingleNode(".//h2")
                             .SelectSingleNode(".//a")
                             .InnerText;
        var images = gallery.SelectSingleNode(".//table").SelectNodes(".//tr")
                            .Select(img => img.SelectSingleNode(".//img").GetSrc().Remove("tn_"))
                            .Select(img => Protocol + img)
                            .ToStringImageLinkWrapperList();

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for bestprettygirl.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> BestPrettyGirlParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='elementor-heading-title elementor-size-large']").InnerText;
        var images = soup.SelectNodes("//img[@class='aligncenter size-full']")
                         .Select(img => img.GetSrc())
                         .Select(dummy => (StringImageLinkWrapper)dummy)
                         .ToList();

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for bitchesgirls.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> BitchesGirlsParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='album-name']").InnerText;
        var images = new List<StringImageLinkWrapper>();
        var baseUrl = CurrentUrl;
        if (baseUrl[^1] != '/')
        {
            baseUrl += "/";
        }
        
        var page = 1;
        while (true)
        {
            if (page != 1)
            {
                soup = await Soupify($"{baseUrl}{page}");
            }

            var posts = soup.SelectSingleNode("//div[@class='albumgrid']")
                            .SelectNodes("./a[@class='post-container']")
                            .Select(post => post.GetHref())
                            .ToStringImageLinkWrapperList();
            images.AddRange(posts);
            var loadBtn = soup.SelectSingleNode("//a[@id='loadMore']");
            if (loadBtn is not null)
            {
                page += 1;
            }
            else
            {
                break;
            }
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for bunkr.si and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> BunkrParse()
    {
        return BunkrParse("");
    }
    
    /// <summary>
    ///     Parses the html for bunkr.si and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> BunkrParse(string url)
    {
        if (url != "")
        {
            CurrentUrl = url;
        }
        
        var soup = await Soupify();
        string dirName;
        if (url == "")
        {
            dirName = soup.SelectSingleNode("//h1[@class='text-[24px] font-bold text-dark dark:text-white']")
                          .InnerText;
        }
        else
        {
            dirName = "internal-use";
        }

        List<StringImageLinkWrapper> images;
        if (CurrentUrl.Contains("/a/"))
        {
            images = [];
            var imagePosts = soup.SelectSingleNode("//div[@class='grid-images']")
                                 .SelectNodes(".//a");
            foreach (var post in imagePosts)
            {
                var href = post.GetHref();
                var ext = href.Split('.')[^1];
                var link = ext switch
                {
                    "mp4" or "webm" or "avi" or "rar" or "zip" or "7z" => $"https://fries.bunkrr.su{href}".Replace("/d/", "/"),
                    _ => post.SelectSingleNode(".//img")
                            .GetSrc()
                            .Replace("/thumbs/", "/")
                            .Replace(".png", $".{ext}")
                };
                images.Add((StringImageLinkWrapper)link);
            }
        }
        else
        {
            var anchor =
                soup.SelectSingleNode(
                    "//a[@class='text-white inline-flex items-center justify-center rounded-[5px] py-2 px-4 text-center text-base font-bold hover:text-white mb-2']");
            var link = anchor.GetHref();
            images = [(StringImageLinkWrapper)link];
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for buondua.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> BuonduaParse()
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true
        });
        var dirName = soup.SelectSingleNode("//div[@class='article-header']")
                          .SelectSingleNode(".//h1")
                          .InnerText;
        var dirNameSplit = dirName.Split("(");
        if (dirName.Contains("pictures") || dirName.Contains("photos"))
        {
            dirName = dirNameSplit[..^1].Join("(");
        }
        
        var pages = soup.SelectSingleNode("//div[@class='pagination-list']")
                       .SelectNodes(".//span")
                       .Count;
        var currUrl = CurrentUrl.Replace("?page=1", "");
        
        var images = new List<StringImageLinkWrapper>();
        for (var i = 0; i < pages; i++)
        {
            var imageList = soup.SelectSingleNode("//div[@class='article-fulltext']")
                               .SelectNodes(".//img")
                               .Select(img => img.GetSrc())
                               .Select(dummy => (StringImageLinkWrapper)dummy)
                               .ToList();
            images.AddRange(imageList);
            if (i >= pages - 1)
            {
                continue;
            }

            var nextPage = $"{currUrl}?page={i + 2}";
            CurrentUrl = nextPage;
            soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
            {
                ScrollBy = true
            });
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for bustybloom.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> BustyBloomParse()
    {
        return GenericHtmlParser("bustybloom");
    }

    /// <summary>
    ///     Parses the html for site and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> CamwhoresParse()
    {
        return CamwhoresParse("");
    }
    
    /// <summary>
    ///     Parses the html for camwhores.tv and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> CamwhoresParse(string url)
    {
        if (url != "")
        {
            CurrentUrl = url;
        }
        
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='headline']").SelectSingleNode(".//h1").InnerText;
        var video = soup.SelectSingleNode(".//div[@class='fp-player']").SelectSingleNode(".//video");
        var videoUrl = video.GetSrc();
        var images = new List<StringImageLinkWrapper> { videoUrl };

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for cherrynudes.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> CherryNudesParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//title")
                          .InnerText
                          .Split("-")[0]
                          .Trim();
        var contentUrl = CurrentUrl.Replace("www", "cdn");
        var images = soup.SelectSingleNode("//div[@class='article__gallery-images']")
                            .SelectNodes(".//a")
                            .Select(img => img.GetHref())
                            .ToStringImageLinkWrapperList();

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for chickteases.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> ChickTeasesParse()
    {
        return GenericBabesHtmlParser("//h1[@id='galleryModelName']", "//div[@class='minithumbs']");
    }

    /// <summary>
    ///     Parses the html for site and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> Cool18Parse()
    {
        var soup = await Soupify();
        var showContent = soup.SelectSingleNode("//td[@class='show_content']");
        var dirName = showContent.SelectSingleNode(".//b").InnerText;
        var images = showContent.SelectSingleNode(".//pre")
                                .SelectNodes(".//img")
                                .Select(img => img.GetSrc())
                                .Select(dummy => (StringImageLinkWrapper)dummy)
                                .ToList();

        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    /// <summary>
    ///     Parses the html for coomer.su and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> CoomerParse()
    {
        return await DotPartyParse("https://coomer.su");
    }

    // TODO: Remove cup-e.club as the site is no longer active

    /// <summary>
    ///     Parses the html for cutegirlporn.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> CuteGirlPornParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='gal-title']").InnerText;
        var images = soup.SelectSingleNode("//ul[@class='gal-thumbs']")
                         .SelectNodes(".//li")
                         .Select(img => "https://cutegirlporn.com" + img.SelectSingleNode(".//img").GetSrc().Replace("/t", "/"))
                         .Select(dummy => (StringImageLinkWrapper)dummy)
                         .ToList();

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for cyberdrop.me and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> CyberDropParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@id='title']").InnerText;
        var imageList = soup.SelectNodes("//div[@class='image-container column']")
                            .Select(image => image
                                            .SelectSingleNode(".//a[@class='image']")
                                            .GetHref())
                            .Select(url => $"https://cyberdrop.me{url}");
        var images = new List<StringImageLinkWrapper>();
        foreach (var image in imageList)
        {
            CurrentUrl = image;
            await WaitForElement("//div[@id='imageContainer']//img", timeout: -1);
            soup = await Soupify();
            var url = soup
                            .SelectSingleNode("//div[@id='imageContainer']")
                            .SelectSingleNode(".//img")
                            .GetSrc();
            images.Add((StringImageLinkWrapper)url);
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for danbooru.donmai.us and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns></returns>
    private async Task<RipInfo> DanbooruParse()
    {
        var tags = Rule34Regex().Match(CurrentUrl).Groups[1].Value;
        tags = Uri.UnescapeDataString(tags);
        var dirName = "[Danbooru] " + tags.Remove("+").Remove("tags=");
        var images = new List<StringImageLinkWrapper>();
        var session = new HttpClient();
        session.DefaultRequestHeaders.Add("User-Agent", "NicheImageRipper");
        var response = await session.GetAsync($"https://danbooru.donmai.us/posts.json?{tags}");
        var json = await response.Content.ReadFromJsonAsync<JsonNode>();
        var data = json!.AsArray();
        var i = 0;
        while (data.Count != 0)
        {
            var urls = data.Select(post => post!["file_url"]!.Deserialize<string>()!);
            images.AddRange(urls.Select(url => (StringImageLinkWrapper)url));
            response = await session.GetAsync($"https://danbooru.donmai.us/posts.json?{tags}&page={i}");
            json = await response.Content.ReadFromJsonAsync<JsonNode>();
            data = json!.AsArray();
            i += 1;
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for decorativemodels.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> DecorativeModelsParse()
    {
        return GenericBabesHtmlParser("//h1[@class='center']", "//div[@class='list gallery']");
    }

    /// <summary>
    ///     Parses the html for deviantart.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> DeviantartParse()
    {
        var soup = await Soupify();
        var dirName = CurrentUrl.Split("/")[3];

        var images = new List<StringImageLinkWrapper>();
        // TODO: Implement the rest of the method
        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    /*
     * def deviantart_parse(self) -> RipInfo:
        """Parses the html for deviantart.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        curr = self.driver.current_url
        dir_name = curr.split("/")[3]
        images = [curr]
        self.driver.quit()
        return RipInfo(images, dir_name, self.filename_scheme)
     */

    /// <summary>
    ///     Parses the html for dirtyyoungbitches.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> DirtyYoungBitchesParse()
    {
        return GenericBabesHtmlParser("//div[@class='title-holder']//h1", 
            "//div[@class='container cont-light']//div[@class='images']//a[@class='thumb']");
    }
    
    private async Task<RipInfo> DropboxParse()
    {
        return await DropboxParse("");
    }

    /// <summary>
    ///     Parses the html for dropbox.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <param name="dropboxUrl"></param>
    /// <returns></returns>
    private async Task<RipInfo> DropboxParse(string dropboxUrl)
    {
        var internalUse = false;
        if (!string.IsNullOrEmpty(dropboxUrl))
        {
            if (dropboxUrl.Contains("/scl/fi/"))
            {
                dropboxUrl = dropboxUrl.Replace("dl=0", "dl=1");
                return new RipInfo([dropboxUrl], "", FilenameScheme);
            }

            CurrentUrl = dropboxUrl;
            internalUse = true;
        }

        var soup = await Soupify(xpath: "//span[@class='dig-Breadcrumb-link-text']");
        string dirName;
        if (!internalUse)
        {
            try
            {
                dirName = soup.SelectSingleNode("//span[@class='dig-Breadcrumb-link-text']").InnerText;
            }
            catch (NullReferenceException)
            {
                var deletedNotice =
                    soup.SelectSingleNode("//h2[@class='dig-Title dig-Title--size-large dig-Title--color-standard']");
                if (deletedNotice is not null)
                {
                    return new RipInfo([], "Deleted", FilenameScheme);
                }

                Print(CurrentUrl);
                throw;
            }
        }
        else
        {
            dirName = "";
        }

        if (CurrentUrl.Contains("/scl/fi/"))
        {
            return new RipInfo([CurrentUrl.Replace("dl=0", "dl=1")], "", FilenameScheme);
        }

        var images = new List<string>();
        var filenames = new List<string>();
        var postsNodes = soup.SelectSingleNode("//ol[@class='_sl-grid-body_6yqpe_26']");
        var posts = new List<string>();
        if (postsNodes is not null)
        {
            posts = postsNodes.SelectNodes("//a").GetHrefs().RemoveDuplicates();
            foreach (var post in posts)
            {
                soup = await Soupify(post, xpath: "//img[@class='_fullSizeImg_1anuf_16']");
                GetDropboxFile(soup, post, filenames, images,
                    posts); // The method modifies the posts list which is being iterated over...
            }
        }
        else
        {
            GetDropboxFile(soup, CurrentUrl, filenames, images, posts);
        }

        return new RipInfo(images.ToWrapperList(), dirName, FilenameScheme, filenames: filenames);
    }
    
    private static void GetDropboxFile(HtmlNode soup, string post, List<string> filenames, List<string> images,
                                       List<string> posts)
    {
        var filename = post.Split("/")[^1].Split("?")[0];
        filenames.Add(filename);
        var img = soup.SelectSingleNode("//img[@class='_fullSizeImg_1anuf_16']");
        try
        {
            if (img is not null)
            {
                var src = img.GetSrc();
                if (src != "")
                {
                    images.Add(src);
                }
            }
            else
            {
                var vid = soup.SelectSingleNode("//video");
                if (vid is not null)
                {
                    var src = vid.SelectSingleNode("//source").GetSrc();
                    if (src != "")
                    {
                        images.Add(src);
                    }
                }
                else
                {
                    var newPosts = soup
                                  .SelectSingleNode("//ol[@class='_sl-grid-body_6yqpe_26']")
                                  .SelectNodes("//a")
                                  .GetHrefs()
                                  .RemoveDuplicates();
                    posts.AddRange(newPosts);
                }
            }
        }
        catch (AttributeNotFoundException)
        {
            // ignored
        }
    }

    /// <summary>
    ///     Parses the html for eahentai.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> EahentaiParse()
    {
        var soup = await Soupify(delay: 1000, lazyLoadArgs: new LazyLoadArgs());
        var dirName = soup.SelectSingleNode("//h2").InnerText;
        var images = soup.SelectSingleNode("//div[@class='gallery']")
                         .SelectNodes(".//a")
                         .Select(img => img
                                       .SelectSingleNode(".//img")
                                       .GetSrc()
                                       .Remove("/thumbnail")
                                       .Replace("t.", "."))
                         .Select(dummy => (StringImageLinkWrapper)dummy)
                         .ToList();

        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    private async Task<RipInfo> EHentaiParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[id='gn']").InnerText;
        var imageLinks = new List<string>();
        var pageCount = 1;
        while (true)
        {
            await PrintAsync($"Parsing page {pageCount}");
            var imageTags = soup.SelectNodes("//div[@class='gdt']//a").GetHrefs();
            imageLinks.AddRange(imageTags);
            var nextPage = soup.SelectSingleNode("//table[@class='ptb']").SelectNodes("//a").GetHrefs().Last();
            if (nextPage == CurrentUrl)
            {
                break;
            }
            
            await Task.Delay(5000);
            try
            {
                pageCount += 1;
                CurrentUrl = nextPage;
            }
            catch (WebDriverTimeoutException)
            {
                await PrintAsync("Timed out. Sleeping for 10 seconds before retrying...", true);
                await Task.Delay(10000);
                CurrentUrl = nextPage;
            }
            soup = await Soupify();
        }
        
        var images = new List<StringImageLinkWrapper>();
        foreach (var (i, link) in imageLinks.Enumerate())
        {
            await PrintAsync($"Parsing image {i + 1}/{imageLinks.Count}");
            await Task.Delay(2500);
            soup = await Soupify(link);
            var img = soup.SelectSingleNode("//img[@id='img']").GetAttributeValue("src", "");
            images.Add(img);
        }
        
        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for 8boobs.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> EightBoobsParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@id='content']")
                          .SelectNodes(".//div[@class='title']")[1]
                          .InnerText;
        var images = soup.SelectSingleNode("//div[@class='gallery clear']")
                            .SelectNodes("./a")
                            .Select(img => Protocol + img
                                        .SelectSingleNode(".//img")
                                        .GetSrc()
                                        .Remove("tn_"))
                            .ToStringImageLinkWrapperList();

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for 8muses.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> EightMusesParse()
    {
        var soup = await Soupify(xpath: "//div[@class='gallery']", lazyLoadArgs: new LazyLoadArgs());
        var dirName = soup.SelectSingleNode("//div[@class='top-menu-breadcrumb']")
                          .SelectNodes(".//a")
                          .Last()
                          .InnerText;
        var images = soup.SelectSingleNode("//div[@class='gallery']")
                            .SelectNodes(".//img")
                            .Select(img => "https://comics.8muses.com" + img.GetSrc().Replace("/th/", "/fm/"))
                            .ToStringImageLinkWrapperList();

        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    // TODO: Remove 8kcosplay.com as all images are behind premium file hosting sites

    /// <summary>
    ///     Parses the html for elitebabes.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> EliteBabesParse()
    {
        return GenericHtmlParser("elitebabes");
    }

    /// <summary>
    ///     Parses the html for erosberry.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> ErosBerryParse()
    {
        return GenericBabesHtmlParser("//h1[@class='title']", "//div[@class='block-post three-post flex']");
    }

    /// <summary>
    ///     Parses the html for erohive.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> EroHiveParse()
    {
        async Task WaitForPostLoad()
        {
            while (true)
            {
                var elm = Driver.FindElement(By.Id("has_no_img"));
                if (elm is not null && elm.GetAttribute("class") != "")
                {
                    await Task.Delay(100);
                    break;
                }

                if(Driver.FindElements(By.XPath("//h2[@class='warning-page']")).Count > 0)
                {
                    await Task.Delay(5000);
                    Driver.Navigate().Refresh();
                }
                
                await Task.Delay(100);
            }
        }
        
        var baseUrl = CurrentUrl.Split("?")[0];
        await WaitForPostLoad();
        var soup = await Soupify();
        var dirName = soup
                     .SelectSingleNode("//h1[@class='title']")
                     .InnerText
                     .Split(" ")
                     .SkipLast(3)
                     .Join(" ");
        var posts = new List<string>();
        var page = 0;
        while (true)
        {
            await PrintAsync($"Parsing page {page}");
            if (page != 0)
            {
                CurrentUrl = $"{baseUrl}?p={page}";
                await WaitForPostLoad();
                soup = await Soupify();
            }

            page += 1;
            var links = soup.SelectNodes("//a[@class='image-thumb']")
                            .Select(link => link.GetHref()).ToList();
            if (links.Count == 0)
            {
                break;
            }

            posts.AddRange(links);
        }
        
        var images = new List<StringImageLinkWrapper>();
        var total = posts.Count;
        foreach (var (i, post) in posts.Enumerate())
        {
            await PrintAsync($"Parsing post {i + 1}/{total}");
            CurrentUrl = post;
            await WaitForPostLoad();
            soup = await Soupify();
            var img = soup.SelectSingleNode("//div[@class='img']")
                         .SelectSingleNode(".//img")
                         .GetSrc();
            images.Add(img);
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for erome.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> EroMeParse()
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            Increment = 1250,
            ScrollBy = true
        });
        var dirName = soup.SelectSingleNode("//h1").InnerText;
        var posts = soup
                   .SelectNodes("//div[@class='col-sm-12 page-content']")[1]
                   .SelectNodes("./div");
        var images = new List<StringImageLinkWrapper>();
        foreach (var post in posts)
        {
            var img = post.SelectSingleNode(".//img");
            if (img is not null)
            {
                var url = img.GetSrc();
                images.Add(url);
                continue;
            }

            var vid = post.SelectSingleNode(".//video");
            if (vid is not null)
            {
                var url = vid.SelectSingleNode(".//source").GetSrc();
                images.Add(url);
            }
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for erothots.co and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> EroThotsParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='head-title']")
                          .SelectSingleNode(".//span")
                          .InnerText;
        var images = soup.SelectSingleNode("//div[@class='album-gallery']")
                         .SelectNodes("./a")
                         .Select(link => link.GetAttributeValue("data-src"))
                         .Select(dummy => (StringImageLinkWrapper)dummy)
                         .ToList();

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for everia.club and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> EveriaParse()
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true
        });
        var dirName = soup.SelectSingleNode("//h1[@class='single-post-title entry-title']").InnerText;
        var images = soup.SelectSingleNode("//figure[@class='wp-block-gallery has-nested-images " +
                                      "columns-1 wp-block-gallery-3 is-layout-flex wp-block-gallery-is-layout-flex']")
                         .SelectNodes(".//img")
                         .Select(img => img.GetSrc())
                         .Select(dummy => (StringImageLinkWrapper)dummy)
                         .ToList();

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for exgirlfriendmarket.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> ExGirlFriendMarketParse()
    {
        return await GenericBabesHtmlParser("//div[@class='title-area']//h1", "//div[@class='gallery']//a[@class='thumb exo']");
    }

    /// <summary>
    ///     Parses the html for fapello.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> FapelloParse()
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250
        });
        var dirName = soup.SelectSingleNode("//h2[@class='font-semibold lg:text-2xl text-lg mb-2 mt-4']").InnerText;
        var images = soup.SelectSingleNode("//div[@id='content']")
                         .SelectNodes(".//img")
                         .Select(img => img.GetSrc().Replace("_300px", ""))
                         .Select(dummy => (StringImageLinkWrapper)dummy)
                         .ToList();

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for faponic.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> FaponicParse()
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true,
            ScrollPauseTime = 1000
        });
        var dirName = soup.SelectSingleNode("//div[@class='author-content']")
                          .SelectSingleNode(".//a")
                          .InnerText;
        var posts = soup.SelectSingleNode("//div[@id='content']")
                        .SelectNodes(".//div[@class='photo-item col-4-width']");
        var images = new List<StringImageLinkWrapper>();
        foreach (var post in posts)
        {
            var video = post.SelectSingleNode(".//a[@class='play-video2']");
            if (video is not null)
            {
                images.Add($"video:{video.GetHref()}");
            }
            else
            {
                images.Add(post.SelectSingleNode(".//img").GetSrc());
            }
        }
        
        foreach(var (i, img) in images.Enumerate())
        {
            if (!img.StartsWith("video:"))
            {
                continue;
            }

            soup = await Soupify(((string)img).Remove("video:"));
            var vid = soup.SelectSingleNode("//source").GetSrc();
            images[i] = vid;
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for f5girls.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> F5GirlsParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectNodes("//div[@class='container']")[2]
                          .SelectSingleNode(".//h1")
                          .InnerText;
        var images = new List<StringImageLinkWrapper>();
        var currUrl = CurrentUrl.Replace("?page=1", "");
        var pages = soup.SelectSingleNode("//ul[@class='pagination']")
                        .SelectNodes(".//li")
                        .Count - 1;
        for (var i = 0; i < pages; i++)
        {
            var imageList = soup.SelectNodes("//img[@class='album-image lazy']")
                                .Select(img => img.GetSrc())
                                .Select(dummy => (StringImageLinkWrapper)dummy)
                                .ToList();
            images.AddRange(imageList);
            if (i >= pages - 1)
            {
                continue;
            }

            var nextPage = $"{currUrl}?page={i + 2}";
            soup = await Soupify(nextPage);
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    // TODO: Fantia support is not yet implemented
    
    /*
     * # Started working on support for fantia.com
    def __fantia_parse(self) -> RipInfo:
        """Parses the html for fantia.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="fanclub-name").find("a").text
        curr_url = self.driver.current_url
        self.current_url = "https://fantia.jp/sessions/signin"
        input("cont")
        self.current_url = curr_url
        post_list = []
        while True:
            posts = soup.find("div", class_="row row-packed row-eq-height").find_all("a", class_="link-block")
            posts = ["https://fantia.jp" + post.get("href") for post in posts]
            post_list.extend(posts)
            next_page = soup.find("ul", class_="pagination").find("a", rel="next")
            if next_page is None:
                break
            else:
                next_page = "https://fantia.jp" + next_page.get("href")
                soup = self.soupify(next_page)
        images = []
        for post in post_list:
            self.current_url = post
            mimg = None
            try:
                mimg = self.driver.find_element(By.CLASS_NAME, "post-thumbnail bg-gray mt-30 mb-30 full-xs ng-scope")
            except selenium.common.exceptions.NoSuchElementException:
                pass
            while mimg is None:
                sleep(0.5)
                try:
                    mimg = self.driver.find_element(By.CLASS_NAME,
                                                    "post-thumbnail bg-gray mt-30 mb-30 full-xs ng-scope")
                except selenium.common.exceptions.NoSuchElementException:
                    pass
            soup = self.soupify()
            self.__print_html(soup)
            print(post)
            main_image = soup.find("div", class_="post-thumbnail bg-gray mt-30 mb-30 full-xs ng-scope").find("img").get(
                "src")
            images.append(main_image)
            # 4 and 6
            other_images = soup.find("div", class_="row no-gutters ng-scope").find_all("img")
            for img in other_images:
                url = img.split("/")
                img_url = "/".join([post, url[4], url[6]])
                images.append(img_url)
        self.driver.quit()
        return RipInfo(images, dir_name, self.filename_scheme)
     */
    
    /// <summary>
    ///     Parses the html for femjoyhunter.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> FemJoyHunterParse()
    {
        return GenericHtmlParser("femjoyhunter");
    }

    /// <summary>
    ///     Parses the html for flickr.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> FlickrParse()
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true
        });
        var dirName = soup.SelectSingleNode("//h1").InnerText;
        var images = new List<StringImageLinkWrapper>();
        var imagePosts = new List<string>();
        var pageCount = 1;
        while (true)
        {
            await PrintAsync($"Parsing page {pageCount}");
            pageCount += 1;
            var posts = soup
                        .SelectSingleNode("//div[contains(@class, 'view') and contains(@class, 'photo-list-view') and contains(@class, 'photostream')]")
                        .SelectNodes(".//div[contains(@class, 'view') and contains(@class, 'photo-list-photo-view') and contains(@class, 'photostream')]")
                        .Select(post => post.SelectSingleNode(".//a[@class='overlay']").GetHref())
                        .Select(dummy => $"https://www.flickr.com{dummy}")
                        .ToList();
            imagePosts.AddRange(posts);
            var nextButton = soup.SelectSingleNode("//a[@rel='next']");
            if (nextButton is not null)
            {
                var nextUrl = nextButton.GetHref();
                soup = await Soupify($"https://www.flickr.com{nextUrl}", lazyLoadArgs: new LazyLoadArgs
                {
                    ScrollBy = true
                });
            }
            else
            {
                break;
            }
        }
        
        foreach (var (i, post) in imagePosts.Enumerate())
        {
            await PrintAsync($"Parsing post {i + 1}: {post}");
            var delay = 100;
            soup = await Soupify(post, delay: delay);
            var script = soup.SelectSingleNode("//script[@class='modelExport']").InnerText;
            string paramValues;
            while (true)
            {
                try
                {
                    paramValues = "{\"photoModel\"" + script.Split("{\"photoModel\"")[1];
                    break;
                }
                catch (IndexOutOfRangeException)
                {
                    delay *= 2;
                    soup = await Soupify(post, delay: delay);
                    script = soup.SelectSingleNode("//script[@class='modelExport']").InnerText;
                }
            }

            paramValues = ExtractJsonObject(paramValues);
            var paramsJson = JsonSerializer.Deserialize<JsonNode>(paramValues);
            var imgUrl = paramsJson?
                        .AsObject()["photoModel"]!
                        .AsObject()["descendingSizes"]!
                        .AsArray()[0]!
                        .AsObject()["url"]
                        .Deserialize<string>()!;
            images.Add(Protocol + imgUrl);
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for foxhq.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> FoxHqParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1").InnerText;
        if (string.IsNullOrEmpty(dirName))
        {
            dirName = soup.SelectSingleNode("//h2").InnerText;
        }

        var url = CurrentUrl;
        var images = soup
                    .SelectNodes("//div[@class='thumb simple']")
                    .Select(img => img.SelectSingleNode(".//a").GetHref())
                    .Select(dummy => (StringImageLinkWrapper)dummy)
                    .ToList();

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for ftvhunter.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> FtvHunterParse()
    {
        return GenericHtmlParser("ftvhunter");
    }
    
    /// <summary>
    ///     Parses the html for gelbooru.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> GelbooruParse()
    {
        var tags = Rule34Regex().Match(CurrentUrl).Groups[1].Value;
        tags = Uri.UnescapeDataString(tags);
        var dirName = "[Gelbooru] " + tags.Replace("+", " ").Replace("tags=", "");
        var session = new HttpClient();
        var response =
            await session.GetAsync($"https://gelbooru.com/index.php?page=dapi&s=post&q=index&json=1&pid=0&{tags}");
        var json = await response.Content.ReadFromJsonAsync<JsonNode>();
        var data = json!["post"]!.AsArray();
        var images = new List<StringImageLinkWrapper>();
        var pid = 1;
        while (true)
        {
            var urls = data.Select(post => post!["file_url"]!.Deserialize<string>()!);
            images.AddRange(urls.Select(url => (StringImageLinkWrapper)url));
            response = await session.GetAsync(
                $"https://gelbooru.com/index.php?page=dapi&s=post&q=index&json=1&pid={pid}&{tags}");
            pid += 1;
            json = await response.Content.ReadFromJsonAsync<JsonNode>();
            var posts = json!["post"];
            if (posts is null)
            {
                break;
            }

            data = posts.AsArray();
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for ggoorr.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> GgoorrParse()
    {
        const string schema = "https://cdn.ggoorr.net";
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1//a").InnerText;
        var posts = soup
                    .SelectSingleNode("//div[@id='article_1']")
                    .SelectSingleNode(".//div")
                    .SelectNodes(".//img|.//video");
        var images = new List<StringImageLinkWrapper>();
        foreach (var post in posts)
        {
            var link = post.GetNullableSrc();
            if (string.IsNullOrEmpty(link))
            {
                link = post.SelectSingleNode(".//source").GetSrc();
            }

            if (!link.Contains("https://"))
            {
                link = $"{schema}{link}";
            }

            images.Add(link);
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for girlsofdesire.org and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> GirlsOfDesireParse()
    {
        return GenericBabesHtmlParser("//a[@class='albumName']", "//div[@id='gal_10']//td[@class='vtop']");
    }

    /// <summary>
    ///     Parses the html for girlsreleased.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> GirlsReleasedParse()
    {
        var soup = await Soupify(delay: 5000);
        var metadata = soup.SelectNodes("//a[@class='separate']");
        var siteName = metadata[0].InnerText.Split(".")[0].ToTitle();
        var modelName = metadata[1].InnerText;
        var setName = metadata[2].InnerText;
        var dirName = $"{{{siteName}}} {setName} [{modelName}]";
        var images = soup
                     .SelectSingleNode("//div[@class='images']")
                     .SelectNodes(".//img")
                     .Select(img => img.GetSrc().Replace("/t/", "/i/").Replace("t.imx", "i.imx"))
                     .Select(dummy => (StringImageLinkWrapper)dummy)
                     .ToList();

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for glam0ur.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> Glam0urParse()
    {
        return GenericBabesHtmlParser("//div[@class='picnav']//h1", "//div[@class='center']/a");
    }

    /// <summary>
    ///     Parses the html for gofile.io and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> GoFileParse()
    {
        await Task.Delay(5000);
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//span[@id='rowFolder-folderName']").InnerText;
        var images = soup
                    .SelectNodes("//div[@id='rowFolder-tableContent']/div")
                    .Select(img => img.SelectSingleNode(".//a[@target='_blank']").GetHref())
                    .ToStringImageLinkWrapperList();
        images.Insert(0, CurrentUrl);

        return new RipInfo(images, dirName, FilenameScheme);
    }

    private async Task<RipInfo> GoogleParse()
    {
        return await GoogleParse("");
    }

    /// <summary>
    ///     Query the google drive API to get file information to download
    /// </summary>
    /// <param name="gdriveUrl">The url to parse (default: CurrentUrl)</param>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> GoogleParse(string gdriveUrl)
    {
        if (string.IsNullOrEmpty(gdriveUrl))
        {
            gdriveUrl = CurrentUrl;
        }

        // Actual querying happens within the RipInfo object
        return Task.FromResult(new RipInfo([gdriveUrl], "", FilenameScheme));
    }

    /// <summary>
    ///     Parses the html for grabpussy.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> GrabPussyParse()
    {
        return GenericBabesHtmlParser("(//div[@class='c-title'])[2]//h1",
            "//div[@class='gal own-gallery-images']/a");
    }

    /// <summary>
    ///     Parses the html for gyrls.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> GyrlsParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='single_title']").InnerText;
        var images = soup
                     .SelectNodes("//div[@id='gallery-1']//a")
                     .Select(img => img.GetHref())
                     .Select(dummy => (StringImageLinkWrapper)dummy)
                     .ToList();

        return new RipInfo(images, dirName, FilenameScheme);
    }

    // TODO: Hanime support is not yet implemented
    
    /*
     * def hanime_parse(self) -> RipInfo:
        """Parses the html for hanime.tv and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        sleep(1)  # Wait so images can load
        soup = self.soupify()
        dir_name = "Hanime Images"
        image_list = soup.find("div", class_="cuc_container images__content flex row wrap justify-center relative") \
            .find_all("a", recursive=False)
        images = [image.get("href") for image in image_list]
        return RipInfo(images, dir_name, self.filename_scheme)
    */

    /// <summary>
    ///     Parses the html for hegrehunter.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> HegreHunterParse()
    {
        return await GenericHtmlParser("hegrehunter");
    }

    /// <summary>
    ///     Parses the html for hentai-cosplays.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> HentaiCosplaysParse()
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true
        });
        var dirName = soup.SelectSingleNode("//div[@id='main_contents']//h2").InnerText;
        var images = new List<StringImageLinkWrapper>();
        while (true)
        {
            var imageList = soup
                           .SelectSingleNode("//div[@id='display_image_detail']")
                           .SelectNodes(".//img")
                           .Select(img => img.GetSrc())
                           .Select(img => HentaiCosplayRegex().Replace(img, ""))
                           .Select(dummy => (StringImageLinkWrapper)dummy)
                           .ToList();
            images.AddRange(imageList);
            var nextPage = soup
                          .SelectSingleNode("//div[@id='paginator']")
                          .SelectNodes(".//span")[^2]
                          .SelectSingleNode(".//a");
            if (nextPage is null)
            {
                break;
            }

            soup = await Soupify($"https://hentai-cosplays.com{nextPage.GetHref()}", lazyLoadArgs: new LazyLoadArgs
            {
                ScrollBy = true
            });
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for hentairox.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> HentaiRoxParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='col-md-7 col-sm-7 col-lg-8 right_details']")
                          .SelectSingleNode(".//h1")
                          .InnerText;
        var images = soup.SelectSingleNode("//div[@id='append_thumbs']")
                         .SelectSingleNode(".//img[@class='lazy preloader']")
                         .GetAttributeValue("data-src");
        var numFiles = int.Parse(soup.SelectSingleNode("//li[@class='pages']").InnerText.Split()[0]);

        return new RipInfo([ images ], dirName, generate: true, numUrls: numFiles);
    }

    /// <summary>
    ///     Parses the html for hustlebootytemptats.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> HustleBootyTempTatsParse()
    {
        var pauseButton = Driver.TryFindElement(By.XPath("//div[@class='galleria-playback-button pause']"));
        pauseButton?.Click();
        var soup = await Soupify(delay: 1000);
        var dirName = soup.SelectSingleNode("//h1[@class='zox-post-title left entry-title']").InnerText;
        var imagesNode = soup.SelectNodes("//div[@class='galleria-thumbnails']//img");
        List<StringImageLinkWrapper> images;
        if (imagesNode is not null)
        {
            images = imagesNode.Select(img => img.GetSrc().Remove("/cache").Split("-nggid")[0])
                               .ToStringImageLinkWrapperList();
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
                        var src = imageNode.GetAttribute("src");
                        if (!seen.Add(src))
                        {
                            continue;
                        }

                        newImages = true;
                        images.Add(src);
                    }

                    nextButton.Click();
                    await Task.Delay(1000);
                }
            }
            else
            {
                var iframe = soup.SelectSingleNode("//iframe");
                var iframeUrl = iframe.GetSrc();
                images = [ iframeUrl ]; // youtube video (probably)
            }
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for hotgirl.asia and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> HotGirlParse()
    {
        if (!CurrentUrl.Contains("stype=slideshow"))
        {
            var urlParts = CurrentUrl.Split("/")[..4];
            CurrentUrl = "/".Join(urlParts) + "/?stype=slideshow";
        }
        
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h3[@itemprop='name']").InnerText;
        var images = soup.SelectNodes("//img[@class='center-block w-100']")
                         .Select(image => image.GetSrc())
                         .Select(dummy => (StringImageLinkWrapper)dummy)
                         .ToList();
        
        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    // TODO: hotpornpics parse cause images weren't loading (cdn issue)
    /*
     * def hotpornpics_parse(self) -> RipInfo:
        """Parses the html for hotpornpics.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="hotpornpics_h1player").text
        images = soup.find("div", class_="hotpornpics_gallerybox").find_all("img")
        images = [img.get("src").replace("-square", "") for img in images]
        self.driver.quit()
        return RipInfo(images, dir_name, self.filename_scheme)
     */

    /// <summary>
    ///     Parses the html for hotstunners.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> HotStunnersParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='title_content']")
                          .SelectSingleNode(".//h2")
                          .InnerText;
        var images = soup.SelectSingleNode("//div[@class='gallery_janna2']")
                            .SelectNodes(".//img")
                            .Select(img => Protocol + img.GetSrc().Remove("tn_"))
                            .ToStringImageLinkWrapperList();
        
        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for hottystop.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> HottyStopParse()
    {
        var soup = await Soupify();
        var boxLargeContent = soup.SelectSingleNode("//div[@class='content-center content-center-2']");
        string dirName;
        try
        {
            dirName = boxLargeContent.SelectSingleNode(".//h1").InnerText;
        }
        catch (NullReferenceException)
        {
            dirName = boxLargeContent.SelectSingleNode(".//u").InnerText;
        }

        var images = soup.SelectSingleNode("//ul[@class='gallery']")
                         .SelectNodes(".//a")
                         .Select(a => a.GetHref())
                         .ToStringImageLinkWrapperList();

        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    // TODO: Remove hqbabes as it's 404
    // TODO: Remove hqsluts as it's down

    /// <summary>
    ///     Parses the html for 100bucksbabes.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> HundredBucksBabesParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='main-col-2']")
                          .SelectSingleNode(".//h2[@class='heading']")
                          .InnerText;
        var images = soup.SelectSingleNode("//div[@class='main-thumbs']")
                            .SelectNodes(".//img")
                            .Select(img => Protocol + img.GetAttributeValue("data-url"))
                            .ToStringImageLinkWrapperList();

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for imgbox.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> ImgBoxParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@id='gallery-view']")
                          .SelectSingleNode(".//h1")
                          .InnerText.Split(" - ")[0];
        var images = soup.SelectSingleNode("//div[@id='gallery-view-content']")
                            .SelectNodes(".//img")
                            .Select(img => img.GetSrc().Replace("thumbs2", "images2").Replace("_b", "_o"))
                            .ToStringImageLinkWrapperList();

        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    /// <summary>
    ///     Parses the html for imgur.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> ImgurParse()
    {
        var clientId = Config.Instance.Keys["Imgur"];
        if (clientId == "")
        {
            Print("Client Id not properly set");
            Print("Follow to generate Client Id: https://apidocs.imgur.com/#intro");
            Print("Then add Client Id to Imgur in config.json under Keys");
            throw new RipperCredentialException("Client Id Not Set");
        }

        RequestHeaders["Authorization"] = "Client-ID " + clientId;
        var albumHash = CurrentUrl.Split("/")[5];
        var session = new HttpClient();
        var request = RequestHeaders.ToRequest(HttpMethod.Get, $"https://api.imgur.com/3/album/{albumHash}");
        var response = await session.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            Print("Client Id is incorrect");
            throw new RipperCredentialException("Client Id Incorrect");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonNode>();
        var jsonData = json!["data"]!.AsObject();
        var dirName = jsonData["title"]!.Deserialize<string>()!;
        var images = jsonData["images"]!.AsArray().Select(img => img!["link"]!.Deserialize<string>()!).ToList();

        return new RipInfo(images.ToWrapperList(), dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for imhentai.xxx and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> ImhentaiParse()
    {
        if (!CurrentUrl.Contains("/gallery/"))
        {
            var galCode = CurrentUrl.Split("/")[4];
            CurrentUrl = $"https://imhentai.xxx/gallery/{galCode}/";
        }

        var soup = await Soupify();
        var images = soup.SelectSingleNode("//img[@class='lazy preloader']").GetAttributeValue("data-src", "");
        if (images == "")
        {
            throw new NotFoundException("Image not found");
        }

        var numPages = int.Parse(soup.SelectSingleNode("//li[@class='pages']").InnerText.Split()[1]);
        var dirName = soup.SelectSingleNode("//h1").InnerText;

        return new RipInfo([images], dirName, generate: true, numUrls: numPages);
    }

    /// <summary>
    ///     Parses the html for influencersgonewild.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> InfluencersGoneWildParse()
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 625,
            ScrollPauseTime = 1000
        });
        var dirName = soup.SelectSingleNode("//h1[@class='g1-mega g1-mega-1st entry-title']").InnerText;
        var posts = soup.SelectSingleNode("//div[@class='g1-content-narrow g1-typography-xl entry-content']")
                        .SelectNodes(".//img|.//video");
        var images = new List<StringImageLinkWrapper>();
        foreach (var post in posts)
        {
            switch (post.Name)
            {
                case "img":
                    var src = post.GetSrc();
                    var url = src.Contains(Protocol) ? src : "https://influencersgonewild.com" + src;
                    images.Add(url);
                    break;
                case "video":
                    images.Add(post.SelectSingleNode(".//source").GetSrc()); // Unable to actually download videos
                    break;
            }
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for inven.co.kr and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> InvenParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='subject ']//span[@class='middle']")
                          .InnerText;
        var images = soup.SelectSingleNode("//div[@id='powerbbsContent']")
                         .SelectNodes(".//img")
                         .Select(img => img.GetSrc().Split("?")[0])
                         .ToStringImageLinkWrapperList();
        
        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for jkforum.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> JkForumParse()
    {
        var soup = await Soupify(delay: 1000);
        var dirName = soup.SelectSingleNode("//div[@class='title-cont']")
                          .SelectSingleNode(".//h1")
                          .InnerText;
        var images = soup.SelectSingleNode("//td[@class='t_f']")
                            .SelectNodes(".//img")
                            .Select(img => img.GetSrc().Remove(".thumb.jpg"))
                            .ToStringImageLinkWrapperList();
        // TODO: Find a way to download videos as well

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for join2babes.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> Join2BabesParse()
    {
        return await GenericBabesHtmlParser("//div[@class='gallery_title_div']//h1", "//div[@class='gthumbs']");
    }

    /// <summary>
    ///     Parses the html for joymiihub.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> JoyMiiHubParse()
    {
        return await GenericHtmlParser("joymiihub");
    }
    
    // TODO: Remove jpg.church as it's down
    
    /// <summary>
    ///     Parses the html for kemono.su and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns></returns>
    private async Task<RipInfo> KemonoParse()
    {
        return await DotPartyParse("https://kemono.su");
    }

    /// <summary>
    ///     Parses the html for leakedbb.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> LeakedBbParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='flow-text left']")
                          .SelectSingleNode(".//h1")
                          .InnerText;
        var imageLinks = soup.SelectSingleNode("//div[@class='post_body scaleimages']")
                            .SelectNodes("./img")
                            .Select(img => img.GetSrc())
                            .ToList();
        var images = new List<StringImageLinkWrapper>();
        foreach (var link in imageLinks)
        {
            if (!link.Contains("postimg.cc"))
            {
                images.Add(link);
                continue;
            }

            soup = await Soupify(link);
            var img = soup.SelectSingleNode("//a[@id='download']").GetHref().Split("?")[0];
            images.Add(img);
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for livejasminbabes.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> LiveJasminBabesParse()
    {
        return await GenericBabesHtmlParser("//div[@id='gallery_header']//h1", "//div[@class='gallery_thumb']");
    }
    
    // TODO: Remove lovefap as it's not loading

    /// <summary>
    ///     Parses the html for luscious.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> LusciousParse()
    {
        if (CurrentUrl.Contains("members."))
        {
            CurrentUrl = CurrentUrl.Replace("members.", "www.");
        }
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='o-h1 album-heading']|//h1[@class='o-h1 video-heading o-padding-sides']").InnerText;
        const string endpoint = "https://members.luscious.net/graphqli/?";
        var albumId = CurrentUrl.Split("/")[4].Split("_")[^1];
        var session = new HttpClient();
        List<StringImageLinkWrapper> images = [];
        Dictionary<string, object> variables;
        string query;
        if (CurrentUrl.Contains("/videos/"))
        {
            variables = new Dictionary<string, object>
            {
                ["id"] = albumId
            };
            query = """
                    query getVideoInfo($id: ID!) {
                        video {
                            get(id: $id) {
                            ... on Video {...VideoStandard}
                            ... on MutationError {errors {code message}}
                            }
                        }
                    }
                    fragment VideoStandard on Video{id title tags content genres description audiences url poster_url subtitle_url v240p v360p v720p v1080p}
                    """;
            var response = await session.PostAsync(endpoint, new StringContent(JsonSerializer.Serialize(new
            {
                operationName = "getVideoInfo",
                query,
                variables
            }), Encoding.UTF8, "application/json"));
            var json = await response.Content.ReadFromJsonAsync<JsonNode>();
            var jsonData = json!["data"]!["video"]!["get"]!;
            string videoUrl;
            if(!jsonData["v1080p"]!.IsNull())
            {
                videoUrl = jsonData["v1080p"]!.Deserialize<string>()!;
            }
            else if(!jsonData["v720p"]!.IsNull())
            {
                videoUrl = jsonData["v720p"]!.Deserialize<string>()!;
            }
            else if(!jsonData["v360p"]!.IsNull())
            {
                videoUrl = jsonData["v360p"]!.Deserialize<string>()!;
            }
            else
            {
                videoUrl = jsonData["v240p"]!.Deserialize<string>()!;
            }
            images.Add(videoUrl);
        }
        else
        {
            variables = new Dictionary<string, object>
            {
                ["input"] = new Dictionary<string, object>
                {
                    ["page"] = 1,
                    ["display"] = "date_newest",
                    ["filters"] = new List<Dictionary<string, string>>
                    {
                        new()
                        {
                            ["name"] = "album_id",
                            ["value"] = albumId
                        }
                    }
                }
            };
            query = """
                    query PictureQuery($input: PictureListInput!) {
                        picture {
                            list(input: $input) {
                                info {
                                    total_items
                                    has_next_page
                                }
                                items {
                                    id
                                    title
                                    url_to_original
                                    tags{
                                        id
                                        text
                                    }
                                }
                            }
                        }
                    }
                    """;
            var nextPage = true;
            while (nextPage)
            {
                var response = await session.PostAsync(endpoint, new StringContent(JsonSerializer.Serialize(new
                {
                    operationName = "PictureQuery",
                    query,
                    variables
                }), Encoding.UTF8, "application/json"));
                var json = await response.Content.ReadFromJsonAsync<JsonNode>();
                var jsonData = json!["data"]!["picture"]!["list"]!;
                nextPage = jsonData["info"]!["has_next_page"]!.Deserialize<bool>();
                var inputDict = (Dictionary<string, object>)variables["input"];
                inputDict["page"] = (int)inputDict["page"] + 1;
                var items = jsonData["items"]!.AsArray();
                images.AddRange(items.Select(item => (StringImageLinkWrapper)item!["url_to_original"]!.Deserialize<string>()!));
            }
        }
        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for mainbabes.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> MainBabesParse()
    {
        return GenericBabesHtmlParser("//div[@class='heading']//h2[@class='title']", "//div[@class='thumbs_box']//div[@class='thumb_box']");
    }

    /// <summary>
    ///     Parses the html for manganato.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> ManganatoParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='story-info-right']")
                          .SelectSingleNode(".//h1")
                          .InnerText;
        var nextChapter = soup.SelectSingleNode("//ul[@class='row-content-chapter']")
                            .SelectNodes("./li")[^1]
                            .SelectSingleNode(".//a");
        var images = new List<StringImageLinkWrapper>();
        var counter = 1;
        while (nextChapter is not null)
        {
            Print($"Parsing Chapter {counter}");
            counter += 1;
            soup = await Soupify(nextChapter.GetHref());
            var chapterImages = soup.SelectSingleNode("//div[@class='container-chapter-reader']")
                                  .SelectNodes(".//img");
            images.AddRange(chapterImages.Select(img => (StringImageLinkWrapper)img.GetSrc()));
            nextChapter = soup.SelectSingleNode("//a[@class='navi-change-chapter-btn-next a-h']");
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    // TODO: Remove maturewoman.xyz as the bulk of the content is behind premium download links

    /// <summary>
    ///     Parses the html for metarthunter.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> MetArtHunterParse()
    {
        return GenericHtmlParser("metarthunter");
    }
    
    // TODO: Remove mitaku.net as it uses ad-based link shortener download links

    /// <summary>
    ///     Parses the html for sex.micmicdoll.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> MicMicDollParse()
    {
        try
        {
            Driver.SwitchTo().Alert().Dismiss();
        }
        catch (NoAlertPresentException)
        {
            // ignored
        }
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h3[@class='post-title entry-title']").InnerText;
        var images = soup.SelectNodes("//div[@class='post-body entry-content']//a")
                         .Select(a => a.GetNullableHref())
                         .Where(item => item is not null)
                         .Select(item => item!)
                         .ToStringImageLinkWrapperList();

        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    /// <summary>
    ///     Parses the html for morazzia.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> MorazziaParse()
    {
        return GenericBabesHtmlParser("//h1[@class='title']", "//div[@class='block-post album-item']//a");
    }

    /// <summary>
    ///     Parses the html for myhentaigallery.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> MyHentaiGalleryParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='comic-description']")
                          .SelectSingleNode(".//h1")
                          .InnerText;
        var images = soup.SelectSingleNode("//ul[@class='comics-grid clear']")
                            .SelectNodes("./li")
                            .Select(img => img.SelectSingleNode(".//img")
                                              .GetSrc()
                                              .Replace("/thumbnail/", "/original/"))
                            .ToStringImageLinkWrapperList();

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for nakedgirls.xxx and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> NakedGirlsParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='content']")
                          .SelectSingleNode(".//h1")
                          .InnerText;
        var images = soup.SelectSingleNode("//div[@class='content']")
                            .SelectNodes(".//div[@class='thumb']")
                            .Select(img => "https://www.nakedgirls.xxx" + img.SelectSingleNode(".//a").GetHref())
                            .ToStringImageLinkWrapperList();

        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    /// <summary>
    ///     Parses the html for rule34.xxx and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> NewgroundsParse()
    {
        // TODO: Test
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true
        };
        var cookieValue = Config.Instance.Cookies["Newgrounds"];
        var cookieJar = Driver.Manage().Cookies;
        cookieJar.DeleteAllCookies();
        cookieJar.AddCookie(new Cookie("vmk1du5I8m", cookieValue));
        var baseUri = CurrentUrl.Split("/")[..3];
        var baseUriString = string.Join("/", baseUri);
        var soup = await Soupify(baseUriString);
        var dirName = soup.SelectSingleNode("//a[@class='user-link']").InnerText.Trim();
        var headerButtons = soup.SelectSingleNode("//div[@class='user-header-buttons']")
                                 .SelectNodes(".//a");
        var hasMovies = false;
        var hasArt = false;
        foreach (var button in headerButtons)
        {
            var href = button.GetHref();
            switch (href)
            {
                case "/movies":
                    hasMovies = true;
                    break;
                case "/art":
                    hasArt = true;
                    break;
            }
        }
        
        var images = new List<StringImageLinkWrapper>();
        if (hasArt)
        {
            soup = await Soupify($"{baseUriString}/art", lazyLoadArgs: lazyLoadArgs);
            var posts = GetPosts(soup, false);
            var numPosts = posts.Count;
            foreach (var (i, post) in posts.Enumerate())
            {
                Print($"Parsing Art Post {i + 1}/{numPosts}");
                soup = await Soupify(post, lazyLoadArgs: lazyLoadArgs, delay: 100);
                var artImages = soup.SelectSingleNode("//div[contains(@class, 'art-images')]");
                if (artImages is not null)
                {
                    var links = artImages.SelectNodes(".//img")
                                         .Select(img => (StringImageLinkWrapper)img.GetSrc());
                    images.AddRange(links);
                }
                else
                {
                    var artViewGallery = soup.SelectSingleNode("//div[@class='art-view-gallery']");
                    if (artViewGallery is not null)
                    {
                        var seen = new HashSet<string>();
                        while (true)
                        {
                            artViewGallery = soup.SelectSingleNode("//div[@class='art-view-gallery']");
                            var container =
                                artViewGallery.SelectSingleNode(".//div[@class='ng-img-container-sync relative']");
                            var anchor = container.SelectSingleNode(".//a");
                            var link = anchor.GetHref();
                            if (!seen.Add(link))
                            {
                                break;
                            }

                            images.Add(link);
                            var nextBtn = Driver.FindElement(By.XPath("//a[@class='gallery-nav right']"));
                            try
                            {
                                nextBtn.Click();
                            }
                            catch (ElementClickInterceptedException)
                            {
                                var blackoutZone = Driver.FindElement(By.XPath("(//div[@class='blackout-bookend'])[3]"));
                                blackoutZone.Click();
                                nextBtn.Click();
                            }

                            await Task.Delay(500);
                            soup = await Soupify();
                        }
                    }
                    else
                    {
                        var img = soup.SelectSingleNode("//div[@class='image']")
                                      .SelectSingleNode(".//img");
                        images.Add(img.GetSrc());
                    }
                }
            }
        }

        if (hasMovies)
        {
            soup = await Soupify($"{baseUriString}/movies", lazyLoadArgs: lazyLoadArgs);
            var posts = GetPosts(soup, true);
            var numPosts = posts.Count;
            foreach (var (i, post) in posts.Enumerate())
            {
                await Task.Delay(100);
                Print($"Parsing Movie Post {i + 1}/{numPosts}");
                CurrentUrl = post;
                await LazyLoad(lazyLoadArgs);
                var videoStart = Driver.TryFindElement(By.XPath("//div[@class='video-barrier']/child::*[2]"));
                if (videoStart is not null)
                {
                    try
                    {
                        videoStart.Click();
                    }
                    catch (ElementClickInterceptedException)
                    {
                        var blackoutZone = Driver.FindElement(By.XPath("(//div[@class='blackout-bookend'])[3]"));
                        blackoutZone.Click();
                        videoStart.Click();
                    }
                    await Task.Delay(500);
                    var optionsBtn = Driver.FindElement(By.XPath("//button[@title='Display Options']"));
                    optionsBtn.Click();
                    var highestRes = Driver.TryFindElement(By.XPath("//div[@class='ng-option-select']/child::*[2]/child::*[1]"));
                    if (highestRes is not null)
                    {
                        var classes = highestRes.GetAttribute("class");
                        if (!classes.Contains("selected"))
                        {
                            highestRes.Click();
                        }
                    }
                    soup = await Soupify();
                    var video = soup.SelectSingleNode("//video");
                    var videoUrl = video.SelectSingleNode(".//source").GetSrc();
                    while (videoUrl.StartsWith("data:"))
                    {
                        await Task.Delay(1000);
                        soup = await Soupify();
                        video = soup.SelectSingleNode("//video");
                        videoUrl = video.SelectSingleNode(".//source").GetSrc();
                    }
                    images.Add(videoUrl);
                }
                else
                {
                    soup = await Soupify();
                    // Assumes the video is an emulated flash video
                    var script = soup.SelectSingleNode("//div[@class='body-guts top']")
                                     .SelectNodes(".//script")[1]
                                     .InnerText;
                    var videoUrl = NewgroundsRegex().Match(script).Value;
                    images.Add(videoUrl);
                }
            }
        }

        return new RipInfo(images, dirName, FilenameScheme);

        List<string> GetPosts(HtmlNode soup, bool movies)
        {
            var posts = new List<string>();
            var postYears = soup.SelectSingleNode("//div[@class='userpage-browse-content']//div")
                                .SelectNodes("./div");
            foreach (var postYear in postYears)
            {
                var postLinks = postYear.SelectNodes(!movies 
                    ? ".//div[@class='span-1 align-center']" 
                    : ".//div[@class='portalsubmission-cell']");

                var postLinksList = postLinks.Select(post => post.SelectSingleNode(".//a").GetHref());
                posts.AddRange(postLinksList);
            }

            return posts;
        }
    }

    /// <summary>
    ///     Parses the html for nhentai.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> NHentaiParse()
    {
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250
        };
        await LazyLoad(lazyLoadArgs);
        var btn = Driver.TryFindElement(By.Id("show-all-images-button"));
        if (btn is not null)
        {
            Driver.ExecuteScript("arguments[0].scrollIntoView();", btn);
            btn.Click();
        }
        await LazyLoad(lazyLoadArgs);
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='title']").InnerText;
        var thumbnails = soup.SelectSingleNode("//div[@class='thumbs']")
                           .SelectNodes(".//img")
                           .Select(img => img.GetNullableAttributeValue("data-src"))
                           .ToList();
        var images = thumbnails.Where(thumb => !string.IsNullOrEmpty(thumb)) // Remove nulls
                               .Select(thumb => NHentaiRegex().Replace(thumb!, "i7."))
                               .Select(newThumb => newThumb.Replace("t.", "."))
                               .ToStringImageLinkWrapperList();

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for nightdreambabe.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> NightDreamBabeParse()
    {
        return GenericBabesHtmlParser("//section[@class='outer-section']//h2[@class='section-title title']",
            "//div[@class='lightgallery thumbs quadruple fivefold']//a[@class='gallery-card']");
    }

    /// <summary>
    ///     Parses the html for nijie.info and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> NijieParse()
    {
        const int delay = 500;
        const int retries = 4;
        SiteName = "nijie";
        await SiteLogin();
        var memberId = NijieRegex().Match(CurrentUrl).Groups[1].Value;
        var soup = await Soupify($"https://nijie.info/members_illust.php?id={memberId}", delay: delay);
        var dirName = soup.SelectSingleNode("//a[@class='name']").InnerText;
        var posts = new List<string>();
        var count = 1;
        while (true)
        {
            Print($"Parsing illustration posts page {count}");
            count++;
            var postTags = soup.SelectSingleNode("//div[@class='mem-index clearboth']")
                                .SelectNodes(".//p[@class='nijiedao']");
            var postLinks = postTags.Select(link => link.SelectSingleNode(".//a").GetHref());
            posts.AddRange(postLinks);
            var nextPageBtn = soup.SelectSingleNode("//div[@class='right']");
            if (nextPageBtn is null)
            {
                break;
            }
            
            nextPageBtn = nextPageBtn.SelectSingleNode(".//p[@class='page_button']");
            if (nextPageBtn is not null)
            {
                var nextPage = nextPageBtn.SelectSingleNode(".//a").GetHref().Replace("&amp;", "&");
                soup = await Soupify($"https://nijie.info{nextPage}", delay: delay);
            }
            else
            {
                break;
            }
        }
        
        Print("Collected all illustration posts...");
        var images = new List<StringImageLinkWrapper>();
        foreach (var (i, post) in posts.Enumerate())
        {
            Print($"Parsing illustration post {i + 1}/{posts.Count}");
            var postId = post.Split("?")[^1];
            soup = await Soupify($"https://nijie.info/view_popup.php?{postId}", delay: delay);
            IEnumerable<StringImageLinkWrapper> imgs = null!;
            for(var retryCount = 0; retryCount < retries; retryCount++)
            {
                try
                {
                    var imageWindow = soup.SelectSingleNode("//div[@id='img_window']");
                    var imageNode = imageWindow.SelectNodes(".//a/img");
                    imgs = imageNode is not null 
                        ? imageNode.Select(img => (StringImageLinkWrapper)(Protocol + img.GetSrc())) 
                        : [(StringImageLinkWrapper)(Protocol + imageWindow.SelectSingleNode(".//video").GetSrc())];
                    break;
                }
                catch (NullReferenceException)
                {
                    await Task.Delay(delay * 10);
                    soup = await Soupify($"https://nijie.info/view_popup.php?{postId}", delay: delay);
                    if (retryCount == retries - 1)
                    {
                        throw new RipperException("Failed to parse illustration post");
                    }
                }
            }
            
            images.AddRange(imgs);
        }
        
        // TODO: Add doujinshi support
        soup = await Soupify($"https://nijie.info/members_dojin.php?id={memberId}", delay: delay);
        posts = [];
        var doujins = soup.SelectSingleNode("//div[@class='mem-index clearboth']")
                          .SelectNodes("./div");
        if (doujins is null)
        {
            return new RipInfo(images, dirName, FilenameScheme);
        }
        
        posts.AddRange(doujins.Select(doujin => doujin.SelectSingleNode(".//a").GetHref()));
        foreach (var (i, post) in posts.Enumerate())
        {
            Print($"Parsing doujin post {i + 1}/{posts.Count}");
            var postId = post.Split("?")[^1];
            soup = await Soupify($"https://nijie.info/view_popup.php?{postId}", delay: delay);
            IEnumerable<StringImageLinkWrapper> imgs = null!;
            for(var retryCount = 0; retryCount < retries; retryCount++)
            {
                try
                {
                    imgs = soup.SelectSingleNode("//div[@id='img_window']")
                               .SelectNodes(".//a/img")
                               .Select(img => (StringImageLinkWrapper)(Protocol + img.GetSrc()));
                    break;
                }
                catch (NullReferenceException)
                {
                    await Task.Delay(delay * 10);
                    soup = await Soupify($"https://nijie.info/view_popup.php?{postId}", delay: delay);
                    if (retryCount == retries - 1)
                    {
                        throw new RipperException("Failed to parse doujin post");
                    }
                }
            }
            images.AddRange(imgs);
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    // TODO: Remove nonsummerjack.com as the site is down

    /// <summary>
    ///     Parses the html for novoglam.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> NovoGlamParse()
    {
        return GenericBabesHtmlParser("//div[@id='heading']//h1", "//ul[@id='myGalleryThumbs']");
    }

    /// <summary>
    ///     Parses the html for novohot.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> NovoHotParse()
    {
        return GenericBabesHtmlParser("//div[@id='viewIMG']//h1", "//div[@class='runout']/a");
    }
    
    // TODO: novojoy.com may be compromised

    /// <summary>
    ///     Parses the html for novoporn.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> NovoPornParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//section[@class='outer-section']")
                          .SelectSingleNode(".//h2")
                          .InnerText
                          .Split("porn")[0]
                          .Trim();
        var images = soup.SelectNodes("//div[@class='thumb grid-item']")
                         .Select(img => img.SelectSingleNode(".//img").GetSrc().Replace("tn_", ""))
                         .ToStringImageLinkWrapperList();

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for nudebird.biz and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> NudeBirdParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='title single-title entry-title']").InnerText;
        var images = soup.SelectNodes("//a[@class='fancybox-thumb']")
                         .Select(img => img.GetHref())
                         .ToStringImageLinkWrapperList();

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for nudity911.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> Nudity911Parse()
    {
        return GenericBabesHtmlParser("//h1", "//tr[@valign='top']//td[@align='center']//table[@width='650']//td[@width='33%']");
    }

    /// <summary>
    ///     Parses the html for omegascans.org and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns></returns>
    private async Task<RipInfo> OmegaScansParse()
    {
        await Task.Delay(5000);
        var soup = await Soupify();
        var dirName = soup
                     .SelectSingleNode(
                          "//h1[@class='text-xl md:text-3xl text-primary font-bold text-center lg:text-left']")
                     .InnerText;
        var chapterCountStr = soup.SelectSingleNode("//div[@class='space-y-2 rounded p-5 bg-foreground']")
                                  .SelectNodes("./div[@class='flex justify-between']")[3]
                                  .SelectSingleNode(".//span[@class='text-secondary line-clamp-1']").InnerText;
        var chapterCount = int.Parse(chapterCountStr.Trim().Split(' ')[0]);
        List<string> chapters = [];
        while (true)
        {
            var links = soup.SelectSingleNode("//ul[@class='grid grid-cols-1 gap-y-8']")
                            .SelectNodes("./a[@href]").GetHrefs().Select(link => $"https://omegascans.org{link}")
                            .ToList();
            chapters.AddRange(links);
            if (chapters.Count == chapterCount)
            {
                break;
            }

            Driver.FindElement(By.XPath("//nav[@class='mx-auto flex w-full justify-center gap-x-2']/ul[last()]//a"))
                  .Click();
            soup = await Soupify();
        }

        chapters.Reverse();
        var images = new List<StringImageLinkWrapper>();
        foreach (var (i, chapter) in chapters.Enumerate())
        {
            Print($"Parsing chapter {i + 1} of {chapterCount}");
            CurrentUrl = chapter;
            await LazyLoad(scrollBy: true, increment: 5000);
            soup = await Soupify();
            var post = soup.SelectSingleNode("//p[@class='flex flex-col justify-center items-center']");
            if (post is null)
            {
                await PrintAsync("Post not found", true);
                continue;
            }

            var imgs = post.SelectNodes("./img[@src]").GetSrcs();
            images.AddRange(imgs.Select(img => (StringImageLinkWrapper)img));
        }

        // Files are numbered per chapter, so original will have the files overwrite each other
        return new RipInfo(images, dirName,
            FilenameScheme == FilenameScheme.Original ? FilenameScheme.Chronological : FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for pbabes.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> PBabesParse()
    {
        return GenericBabesHtmlParser("(//div[@class='box_654'])[2]//h1", "//div[@style='margin-left:35px;']//a[@rel='nofollow']");
    }
    
    // TODO: Remove pinkfineart.com as site is down

    private Task<RipInfo> PixelDrainParse()
    {
        return PixelDrainParse("");
    }
    
    /// <summary>
    ///     Parses the html for site and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> PixelDrainParse(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            url = CurrentUrl;
        }
        var apiKey = Config.Instance.Keys["Pixeldrain"];
        var counter = 0;
        var images = new List<StringImageLinkWrapper>();
        string dirName;
        var client = new HttpClient();
        if (url.Contains("/l/"))
        {
            var id = url.Split("/")[4].Split("#")[0];
            var response = await client.GetAsync($"https://pixeldrain.com/api/list/{id}");
            var responseJson = await response.Content.ReadFromJsonAsync<JsonNode>();
            dirName = responseJson!["title"]!.Deserialize<string>()!;
            var files = responseJson["files"]!.AsArray();
            foreach (var file in files)
            {
                var link = new ImageLink(file!["id"]!.Deserialize<string>()!, FilenameScheme, counter,
                    filename: file["name"]!.Deserialize<string>()!, linkInfo: LinkInfo.PixelDrain);
                counter++;
                images.Add(link);
            }
        }
        else if (url.Contains("/u/"))
        {
            var id = url.Split("/")[4];
            var response = await client.GetAsync($"https://pixeldrain.com/api/file/{id}/info");
            var responseJson = await response.Content.ReadFromJsonAsync<JsonNode>();
            dirName = responseJson!["id"]!.Deserialize<string>()!;
            var link = new ImageLink(responseJson["id"]!.Deserialize<string>()!, FilenameScheme, counter,
                filename: responseJson["name"]!.Deserialize<string>()!, linkInfo: LinkInfo.PixelDrain);
            images.Add(link);
        }
        else
        {
            throw new RipperException($"Unknown url: {url}");
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for pleasuregirl.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> PleasureGirlParse()
    {
        return GenericBabesHtmlParser("//h2[@class='title']",
            "//div[@class='lightgallery-wrap']//div[@class='grid-item thumb']");
    }

    /// <summary>
    ///     Parses the html for pmatehunter.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> PMateHunterParse()
    {
        return GenericHtmlParser("pmatehunter");
    }

    /// <summary>
    ///     Parses the html for porn3dx.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> Porn3dxParse()
    {
        const int maxRetries = 4;
        var cookie = Config.Instance.Cookies["Porn3dx"];
        var cookieJar = Driver.Manage().Cookies;
        cookieJar.DeleteCookieNamed("porn3dx_session");
        cookieJar.AddCookie(new Cookie("porn3dx_session", cookie));
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250,
            ScrollPauseTime = 1000
        };
        var soup = await Soupify(lazyLoadArgs: lazyLoadArgs);
        var dirName = soup.SelectSingleNode("//div[@class='items-center self-center text-sm font-bold leading-none text-white ']")
                          .InnerText;
        var origUrl = CurrentUrl;
        var posts = new List<string>();
        var id = 0;
        if (!await WaitForElement("//a[@id='gallery-0']", timeout: 50))
        {
            throw new RipperException("Element could not be found");
        }

        while (true)
        {
            var post = Driver.TryFindElement(By.Id($"gallery-{id}"));
            if (post is null)
            {
                break;
            }

            posts.Add(post.GetAttribute("href"));
            id++;
        }
        
        var images = new List<StringImageLinkWrapper>();
        foreach (var (i, post) in posts.Enumerate())
        {
            var contentFound = false;
            Print($"Parsing post {i + 1} of {posts.Count}");
            while (!contentFound)
            {
                CurrentUrl = post;
                await Task.Delay(100);
                var iframes = Driver.FindElements(By.XPath("//main[@id='postView']//iframe"));
                var pictures = Driver.FindElements(By.XPath("//picture"));
                while (iframes?.Count == 0 && pictures?.Count == 0)
                {
                    await Task.Delay(5000);
                    if (CurrentUrl == origUrl)
                    {
                        var ad = Driver.TryFindElement(By.XPath("//div[@class='ex-over-top ex-opened']//div[@class='ex-over-btn']"));
                        ad?.Click();
                    }

                    CleanTabs("porn3dx");
                    iframes = Driver.FindElements(By.XPath("//main[@id='postView']//iframe"));
                    pictures = Driver.FindElements(By.XPath("//picture"));
                }

                if (iframes?.Count != 0)
                {
                    foreach (var iframe in iframes!)
                    {
                        var iframeUrl = iframe.GetAttribute("src");
                        if (!iframeUrl.Contains("iframe.mediadelivery.net"))
                        {
                            continue;
                        }

                        contentFound = true;
                        Driver.SwitchTo().Frame(iframe);
                        string url = null!;
                        var maxQuality = 0;
                        for (var _ = 0; _ < maxRetries; _++)
                        {
                            var source = Driver.TryFindElement(By.XPath("//video/source")) ?? Driver.FindElement(By.XPath("//video"));
                            url = source.GetAttribute("src");
                            if (url.StartsWith("blob:") || url == "")
                            {
                                await Task.Delay(500);
                                continue;
                            }
                            var qualities = Driver.FindElements(By.XPath("//button[@data-plyr='quality']"));
                            maxQuality = qualities.Select(quality => int.Parse(quality.GetAttribute("value")))
                                                  .Prepend(0)
                                                  .Max();
                            break;
                        }

                        images.Add($"{url}{{{maxQuality}}}{iframeUrl}");
                        Driver.SwitchTo().DefaultContent();
                    }
                }

                if (pictures?.Count != 0)
                {
                    foreach (var picture in pictures!)
                    {
                        contentFound = true;
                        var imgs = picture.FindElements(By.XPath(".//img"));
                        images.AddRange(imgs.Select(img => img.GetAttribute("src"))
                                            .Where(url => url.Contains("m.porn3dx.com") && !url.Contains("avatar") 
                                                 && !url.Contains("thumb"))
                                            .Select(url => (StringImageLinkWrapper)url));
                    }
                }
            }
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for pornhub.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> PornhubParse()
    {
        var cookie = Config.Instance.Cookies["Pornhub"];
        var cookieJar = Driver.Manage().Cookies;
        cookieJar.AddCookie(new Cookie("il", cookie));
        cookieJar.AddCookie(new Cookie("accessAgeDisclaimerPH", "1"));
        cookieJar.AddCookie(new Cookie("adBlockAlertHidden", "1"));
        CurrentUrl = CurrentUrl; // Refreshes the page
        var soup = await Soupify();
        string dirName;
        List<StringImageLinkWrapper> images;
        if (CurrentUrl.Contains("view_video"))
        {
            dirName = soup.SelectSingleNode("//h1[@class='title']").SelectSingleNode(".//span").InnerText;
            var player = soup.SelectSingleNode("//div[@id='player']").SelectSingleNode(".//script");
            var js = player.InnerText;
            js = js.Split("var ")[1];
            var start = js.IndexOf('{');
            var rawJson = ExtractJsonObject(js[start..]);
            var jsonData = JsonSerializer.Deserialize<JsonNode>(rawJson);
            var mediaDefinitions = jsonData!["mediaDefinitions"]!.AsArray();
            var highestQuality = 0;
            var highestQualityUrl = "";
            foreach (var definition in mediaDefinitions)
            {
                var qualityJson = definition!["quality"]!;
                if (qualityJson.IsArray())
                {
                    continue;
                }
                
                var quality = qualityJson.Deserialize<string>()!.ToInt();
                if (quality > highestQuality)
                {
                    highestQuality = quality;
                    highestQualityUrl = definition["videoUrl"]!.Deserialize<string>()!;
                }
            }

            images = [highestQualityUrl];
        }
        else if (CurrentUrl.Contains("/album/"))
        {
            await LazyLoad(scrollBy: true);
            soup = await Soupify();
            dirName = soup.SelectSingleNode("//h1[@class='photoAlbumTitleV2']").InnerText.Trim();
            var posts = soup.SelectSingleNode("//ul[@class='photosAlbumsListing albumViews preloadImage']")
                            .SelectNodes(".//a")
                            .Select(a => "https://www.pornhub.com" + a.GetHref());
            images = [];
            foreach (var post in posts)
            {
                CurrentUrl = post;
                await WaitForElement("//div[@id='photoImageSection']//img|//video[@class='centerImageVid']");
                soup = await Soupify();
                var imageNode = soup.SelectSingleNode("//div[@id='photoImageSection']//img");
                var url = imageNode is not null 
                    ? imageNode.GetSrc() 
                    : soup.SelectSingleNode("//video[@class='centerImageVid']/source").GetSrc();
                images.Add(url);
            }
        }
        else // Assume gif
        {
            dirName = soup.SelectSingleNode("//div[@class='gifTitle']/h1")?.InnerText ?? "";
            if (dirName == "")
            {
                var id = CurrentUrl.Split("/")[4];
                dirName = $"Pornhub Gif {id}";
            }
            await WaitForElement("//video[@id='gifWebmPlayer']/source", timeout: -1);
            soup = await Soupify();
            var url = soup.SelectSingleNode("//video[@id='gifWebmPlayer']/source").GetSrc();
            images = [url];
        }
        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for putmega.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> PutMegaParse()
    {
        const int maxRetries = 4;
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//a[@data-text='album-name']").InnerText;
        var images = new List<StringImageLinkWrapper>();
        while (true)
        {
            var imageList = soup.SelectSingleNode("//div[@class='pad-content-listing']")
                                .SelectNodes(".//img")
                                .Select(img => (StringImageLinkWrapper)img.GetSrc().Remove(".md"));
            images.AddRange(imageList);
            var nextPage = soup.SelectSingleNode("//li[@class='pagination-next']");
            if (nextPage is null)
            {
                break;
            }

            var nextPageUrl = nextPage.SelectSingleNode(".//a").GetNullableHref();
            if (string.IsNullOrEmpty(nextPageUrl))
            {
                break;
            }
            
            soup = await Soupify("https://putmega.com" + nextPageUrl.Replace("&amp;", "&"), delay: 250);
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for rabbitsfun.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private  Task<RipInfo> RabbitsFunParse()
    {
        return GenericBabesHtmlParser("//h3[@class='watch-mobTitle']", "//div[@class='gallery-watch']//li");
    }
    
    /// <summary>
    ///     Parses the html for redgifs.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> RedGifsParse()
    {
        await Task.Delay(3000);
        const string baseRequest = "https://api.redgifs.com/v2/gifs?ids=";
        await LazyLoad(scrollBy: true, increment: 1250);
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='userName']").InnerText;
        var images = new List<StringImageLinkWrapper>();
        var posts = soup.SelectSingleNode("//div[@class='tileFeed']").SelectNodes("./a[@href]").GetHrefs();
        var ids = posts.Select(post => post.Split("/")[^1].Split("#")[0]).ToList();
        var idChunks = ids.Chunk(100);
        var session = new HttpClient();
        var token = await TokenManager.Instance.GetToken("redgifs");
        RequestHeaders["Authorization"] = $"Bearer {token.Value}";
        foreach (var chunk in idChunks)
        {
            var idParam = string.Join("%2C", chunk);
            var request = RequestHeaders.ToRequest(HttpMethod.Get, $"{baseRequest}{idParam}");
            var response = await session.SendAsync(request);
            var responseJson = (await response.Content.ReadFromJsonAsync<JsonNode>())!;
            var gifs = responseJson["gifs"]!.AsArray();
            images.AddRange(gifs
                           .Select(gif => gif!["urls"]!["hd"]!
                               .Deserialize<string>())
                           .Select(gifUrl => gifUrl!)
                           .Select(dummy => (StringImageLinkWrapper)dummy));
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for redpornblog.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> RedPornBlogParse()
    {
        return GenericBabesHtmlParser("//div[@id='pic-title']//h1", "//div[@id='bigpic-image']");
    }

    /// <summary>
    ///     Parses the html for rossoporn.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> RossoPornParse()
    {
        return GenericBabesHtmlParser("//div[@class='content_right']//h1", "//div[@class='wrapper_g']");
    }
    
    /// <summary>
    ///     Parses the html for rule34.xxx and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns></returns>
    private async Task<RipInfo> Rule34Parse()
    {
        var tags = Rule34Regex().Match(CurrentUrl).Groups[1].Value;
        tags = Uri.UnescapeDataString(tags);
        var dirName = "[Rule34] " + tags.Replace("+", " ").Replace("tags=", "");
        var session = new HttpClient();
        var response =
            await session.GetAsync($"https://api.rule34.xxx/index.php?page=dapi&s=post&q=index&json=1&pid=0&{tags}");
        var json = await response.Content.ReadFromJsonAsync<JsonNode>();
        var data = json!.AsArray();
        var images = new List<StringImageLinkWrapper>();
        var pid = 1;
        while (data.Count != 0)
        {
            var urls = data.Select(post => post!["file_url"]!.Deserialize<string>()!);
            images.AddRange(urls.Select(url => (StringImageLinkWrapper)url));
            response = await session.GetAsync(
                $"https://api.rule34.xxx/index.php?page=dapi&s=post&q=index&json=1&pid={pid}&{tags}");
            pid += 1;
            json = await response.Content.ReadFromJsonAsync<JsonNode>();
            data = json!.AsArray();
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    /// <summary>
    ///     Parses the html for sankakucomplex.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns></returns>
    private async Task<RipInfo> SankakuComplexParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//header[@class='entry-header']/h1[@class='entry-title']/a").InnerText;
        var imagesBase = soup.SelectNodes("//a[@class='swipebox']").GetHrefs()[1..];
        var images = imagesBase.Select(image => !image.Contains("http") ? $"https:{image}" : image)
                               .Select(dummy => (StringImageLinkWrapper)dummy).ToList();
        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for sensualgirls.org and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> SensualGirlsParse()
    {
        return GenericBabesHtmlParser("//a[@class='albumName']", "//div[@id='box_289']//div[@class='gbox']");
    }

    /// <summary>
    ///     Parses the html for sexhd.pics and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> SexHdParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='photobig']//h4")
                          .InnerText
                          .Split(":")[1]
                          .Trim();
        var images = soup.SelectNodes("//div[@class='photobig']//div[@class='relativetop']")
                         .Skip(1)
                         .Select(img => $"https://sexhd.pics{img.SelectSingleNode(".//a").GetHref()}")
                         .ToStringImageLinkWrapperList();

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for sexyaporno.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> SexyAPornoParse()
    {
        return GenericHtmlParser("sexyaporno");
    }

    /// <summary>
    ///     Parses the html for sexybabesart.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> SexyBabesArtParse()
    {
        return GenericBabesHtmlParser("//div[@class='content-title']/h1", "//div[@class='thumbs']");
    }

    /// <summary>
    ///     Parses the html for sexykittenporn.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> SexyKittenPornParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='blockheader']").InnerText;
        var tagList = soup.SelectNodes("//div[@class='list gallery col3']")
                          .SelectMany(tag => tag.SelectNodes(".//div[@class='item']"));
        var imageLink = tagList.Select(image => 
            $"https://www.sexykittenporn.com{image.SelectSingleNode(".//a").GetHref()}");
        var images = new List<StringImageLinkWrapper>();
        foreach (var link in imageLink)
        {
            soup = await Soupify(link);
            images.Add($"https:{soup.SelectSingleNode("//div[@class='image-wrapper']//img")
                               .GetSrc()}");
        }
        
        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for sexynakeds.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> SexyNakedsParse()
    {
        return GenericBabesHtmlParser("(//div[@class='box']//h1)[2]", "//div[@class='post_tn']");
    }

    /// <summary>
    ///     Parses the html for sfmcompile.club and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> SfmCompileParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='g1-alpha g1-alpha-2nd page-title archive-title']")
                          .InnerText
                          .Replace("\"", "");
        var elements = new List<HtmlNode>();
        var images = new List<StringImageLinkWrapper>();
        while (true)
        {
            var items = soup.SelectSingleNode("//ul[@class='g1-collection-items']")
                            .SelectNodes(".//li[@class='g1-collection-item']");
            elements.AddRange(items);
            var nextPage = soup.SelectSingleNode("//a[@class='g1-link g1-link-m g1-link-right next']");
            if (nextPage is null)
            {
                break;
            }
            
            var nextPageUrl = nextPage.GetHref();
            soup = await Soupify(nextPageUrl);
        }
        
        foreach (var element in elements)
        {
            string videoSrc;
            var media = element.SelectSingleNode(".//video");
            if (media is not null)
            {
                videoSrc = media.SelectSingleNode(".//a").GetHref();
                images.Add(videoSrc);
            }
            else
            {
                var videoLink = element.SelectSingleNode(".//a[@class='g1-frame']").GetHref();
                soup = await Soupify(videoLink);
                videoSrc = soup.SelectSingleNode("//video")
                                   .SelectSingleNode(".//source")
                                   .GetSrc();
            }
            images.Add(videoSrc);
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for silkengirl.com and silkengirl.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> SilkenGirlParse()
    {
        return GenericBabesHtmlParser("//h1[@class='title']|//div[@class='content_main']//h2",
            "//div[@class='thumb_box']");
    }
    
    /*

    // TODO:
    def simpcity_parse(self) -> RipInfo:
        """
            Parses the html for simpcity.su and extracts the relevant information necessary for
            downloading images from the site
        """
        self.site_login()
        self.lazy_load(scroll_by=True, increment=1250)
        soup = self.soupify()
        dir_name = soup.find("h1", class_="p-title-value").text
        images = []
        page_count = 1
        while True:
            print(f"Parsing page {page_count}")
            page_count += 1
            posts = soup.find("div", class_="block-body js-replyNewMessageContainer").find_all("article", recursive=False)
            for post in posts:
                imgs = post.find_all("img")
                for img in imgs:
                    url = img.get("src").replace(".md.", ".")
                    images.append(url)
                iframes = post.find_all("iframe", class_="saint-iframe")
                # TODO: Fix iframe switching
                for i, _ in enumerate(iframes):
                    with open("iframe.html", "w") as f:
                        f.write(str(iframes[i]))
                    iframe = self.driver.find_elements(By.XPATH, f'//iframe[@class="saint-iframe"]')[i]
                    print(str(iframe))
                    self.driver.switch_to.frame(iframe)
                    soup = self.soupify()
                    video = soup.find("video", id="main-video")
                    url = video.find("source").get("src")
                    images.append(url)
                    self.driver.switch_to.default_content()
                    soup = self.soupify()
                anchor_divs = post.find("div", class_="bbWrapper").find_all("div", recursive=False)
                for div in anchor_divs:
                    anchor = div.find("a")
                    url = anchor.get("href")
                    if "bunkrr." in url:
                        links = self.bunkrr_parse(url)
                        images.extend(links)
                    elif "camwhores.tv" in url:
                        links = self.camwhores_parse(url)
                        images.extend(links)
                    else:
                        with open("external_links.txt", "a") as f:
                            f.write(url + "\n")
            next_btn = soup.find("a", class_="pageNav-jump pageNav-jump--next")
            if next_btn is None:
                break
            else:
                next_page = next_btn.get("href")
                soup = self.soupify(f"https://simpcity.su{next_page}")
        return RipInfo(images, dir_name, self.filename_scheme, discard_blob=True)
    */

    /// <summary>
    ///     Parses the html for simply-cosplay.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> SimplyCosplayParse()
    {
        await Task.Delay(5000);
        var viewButton = Driver.TryFindElement(By.XPath("//button[@class='btn btn-default']"));
        viewButton?.Click();
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='content-headline']").InnerText;
        var imageList = soup.SelectSingleNode("//div[@class='swiper-wrapper']");
        List<StringImageLinkWrapper> images;
        if (imageList is null)
        {
            images = [soup.SelectSingleNode("//div[@class='image-wrapper']//img")
                          .GetSrc()];
        }
        else
        {
            images = soup.SelectSingleNode("//section/div[@class='row vertical-gutters']")
                         .SelectNodes(".//img")
                         .Select(url => url.GetAttributeValue("data-src").Remove("thumb_"))
                         .ToStringImageLinkWrapperList();
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for sxchinesegirlz01.xyz and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> SxChineseGirlz01Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='post-title entry-title']").InnerText;
        var numPages = soup.SelectSingleNode("//div[@class='page-links']")
                          .SelectNodes("./a")
                          .Count + 1;
        var images = new List<StringImageLinkWrapper>();
        var baseUrl = CurrentUrl;
        for (var i = 0; i < numPages; i++)
        {
            if (i != 0)
            {
                soup = await Soupify($"{baseUrl}{i + 1}/");
            }

            var imageList = soup.SelectSingleNode("//div[@class='entry-content gridlane-clearfix']")
                                .SelectNodes("./figure[@class='wp-block-image size-large']")
                                .Select(img => img.SelectSingleNode(".//img").GetSrc());
            images.AddRange(imageList.Select(img => SxChineseGirlzRegex().Replace(img, ""))
                                     .Select(imageUrl => (StringImageLinkWrapper)imageUrl));
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for theomegaproject.org and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> TheOmegaProjectParse()
    {
        // TODO: Test
        // var soup = await Soupify();
        // var dirName = soup.SelectSingleNode("//h1[@class='omega']").InnerText;
        // var images = soup.SelectSingleNode("//div[@class='postholder']")
        //                  .SelectNodes(".//div[@class='picture']")
        //                  .Select(img => $"{PROTOCOL}{img.SelectSingleNode(".//img").GetSrc().Remove("tn_")}")
        //                  .ToStringImageLinkWrapperList();
        //
        // return new RipInfo(images, dirName, FilenameScheme);
        return await GenericBabesHtmlParser("//h1[@class='omega']", "//div[@class='postholder']//div[@class='picture']");
    }

    /// <summary>
    ///     Parses the html for thothub.lol and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> ThothubParse()
    {
        // TODO: Test
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 625,
            ScrollPauseTime = 1
        };
        var soup = await Soupify(lazyLoadArgs: lazyLoadArgs);
        var dirName = soup.SelectSingleNode("//div[@class='headline']")
                          .SelectSingleNode(".//h1")
                          .InnerText;
        List<StringImageLinkWrapper> images;
        if (CurrentUrl.Contains("/videos/"))
        {
            var vid = soup.SelectSingleNode("//video[@class='fp-engine']")
                          .GetSrc();
            if (string.IsNullOrEmpty(vid))
            {
                vid = soup.SelectSingleNode("//div[@class='no-player']")
                          .SelectSingleNode(".//img")
                          .GetSrc();
            }

            images = [vid];
        }
        else
        {
            var posts = soup.SelectSingleNode("//div[@class='images']")
                            .SelectNodes(".//img")
                            .Select(img => img.GetSrc().Replace("/main/200x150/", "/sources/"));
            images = posts.ToStringImageLinkWrapperList();
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    /*

    def thothub_parse(self) -> RipInfo:
        """
            Parses the html for thothub.lol and extracts the relevant information necessary for downloading images from the site
        """
        self.lazy_load(True, increment=625, scroll_pause_time=1)
        soup = self.soupify()
        dir_name = soup.find("div", class_="headline").find("h1").text
        if "/videos/" in self.current_url:
            vid = soup.find("video", class_="fp-engine").get("src")
            if not vid:
                vid = soup.find("div", class_="no-player").find("img").get("src")
            images = [vid]
        else:
            posts = soup.find("div", class_="images").find_all("img")
            images = []
            for img in posts:
                url = img.get("src").replace("/main/200x150/", "/sources/")
                images.append(url)
        return RipInfo(images, dir_name, self.filename_scheme)

    def thotsbay_parse(self) -> RipInfo:
        """Parses the html for thotsbay.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="album-info-title").find("h1").text
        images = []
        while not images:
            soup = self.soupify()
            images = soup.find("div", class_="album-files").find_all("a")
            images = [img.get("href") for img in images]
            sleep(1)
        vid = []
        for i, link in enumerate(images):
            if "/video/" in link:
                vid.append(i)
                soup = self.soupify(link)
                images.append(soup.find("video", id="main-video").find("source").get("src"))
        for i in reversed(vid):
            images.pop(i)
        self.driver.quit()
        return RipInfo(images, dir_name, self.filename_scheme)

    def tikhoe_parse(self) -> RipInfo:
        """Parses the html for tikhoe.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="album-title").find("h1").text
        file_tag = soup.find("div", class_="album-files")
        images = file_tag.find_all("a")
        videos = file_tag.find_all("source")
        images = [img.get("href") for img in images]
        videos = [vid.get("src") for vid in videos]
        images.extend(videos)
        self.driver.quit()
        return RipInfo(images, dir_name, self.filename_scheme)

    def titsintops_parse(self) -> RipInfo:
        """
            Parses the html for titsintops.com and extracts the relevant information necessary for downloading images from the site
        """
        # Parses the html of the site
        # noinspection PyPep8Naming
        SITE_URL = "https://titsintops.com"
        self.site_login()
        cookies = self.driver.get_cookies()
        cookie_str = ''
        for c in cookies:
            cookie_str += "".join([c['name'], '=', c['value'], ';'])
        requests_header["cookie"] = cookie_str
        soup = self.soupify()
        dir_name = soup.find("h1", class_="p-title-value").text
        images = []
        external_links: dict[str, list[str]] = self.__create_external_link_dict()
        page_count = 1
        while True:
            print(f"Parsing page {page_count}")
            page_count += 1
            posts = soup.find("div", class_="block-body js-replyNewMessageContainer").find_all("div",
                                                                                               class_="message-content js-messageContent")
            for post in posts:
                imgs = post.find("article", class_="message-body js-selectToQuote").find_all("img")
                if imgs:
                    imgs = [im.get("src") for im in imgs if "http" in im]
                    images.extend(imgs)
                videos = post.find_all("video")
                if videos:
                    video_urls = [f"{SITE_URL}{vid.find('source').get('src')}" for vid in videos]
                    images.extend(video_urls)
                iframes = post.find_all("iframe")
                if iframes:
                    # with open("test.html", "w") as f:
                    #     f.write(str(iframes))
                    embedded_urls = [em.get("src") for em in iframes]
                    embedded_urls = self.parse_embedded_urls(embedded_urls)
                    images.extend(embedded_urls)
                attachments = post.find("ul", class_="attachmentList")
                if attachments:
                    attachments = attachments.find_all("a", class_="file-preview js-lbImage")
                    if attachments:
                        attachment_urls = [f"{SITE_URL}{attach.get('href')}" for attach in attachments]
                        images.extend(attachment_urls)
                links = post.find("article", class_="message-body js-selectToQuote").find_all("a")
                if links:
                    links = [link.get("href") for link in links]
                    # print(links)
                    filtered_links = self.__extract_external_urls(links)
                    downloadable_links = self.__extract_downloadable_links(filtered_links, external_links)
                    images.extend(downloadable_links)
            next_page = soup.find("a", class_="pageNav-jump pageNav-jump--next")
            if next_page:
                next_page = next_page.get("href")
                soup = self.soupify(f"{SITE_URL}{next_page}")
            else:
                self.__save_external_links(external_links)
                break
        return RipInfo(images, dir_name, self.filename_scheme)

    def toonily_parse(self) -> RipInfo:
        """
            Parses the html for toonily.me and extracts the relevant information necessary for downloading images from the site
        """
        #self.__wait_for_element('//div[@id="show-more-chapters"]', timeout=-1)
        btn = self.try_find_element(By.XPATH, '//div[@id="show-more-chapters"]')
        if btn is not None:
            self.scroll_element_into_view(btn)
            btn.click()
            sleep(1)
        soup = self.soupify()
        dir_name = soup.find("div", class_="name box").find("h1").text
        chapter_list = soup.find("ul", id="chapter-list").find_all("li", recursive=False)
        chapters = [f"https://toonily.me{chapter.find('a').get('href')}" for chapter in chapter_list]
        images = []
        for chapter in reversed(chapters):
            soup = self.soupify(chapter, lazy_load_args={"scroll_by": True, "increment": 5000})
            image_list = soup.find("div", id="chapter-images").find_all("img")
            images.extend([img.get("src") for img in image_list])

        return RipInfo(images, dir_name, self.filename_scheme)

    def tsumino_parse(self) -> RipInfo:
        """Parses the html for tsumino.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="book-title").text
        num_pages = int(soup.find("div", id="Pages").text.strip())
        pager_url = self.driver.current_url.replace("/entry/", "/Read/Index/") + "?page="
        images = []
        for i in range(1, num_pages + 1):
            soup = self.soupify(pager_url + str(i), 3)
            src = soup.find("img", class_="img-responsive reader-img").get("src")
            images.append(src)
        return RipInfo(images, dir_name, self.filename_scheme)

    def twitter_parse(self) -> RipInfo:
        """
            Parses the html for twitter.com/x.com and extracts the relevant information necessary for downloading images from the site
        """
        
        # region Method-Global Variables

        true_base_wait_time = 2.5
        base_wait_time = true_base_wait_time
        max_wait_time = 60
        wait_time = base_wait_time
        max_retries = 4

        # endregion

        def twitter_parse_helper(post_link: str, log_failure: bool) -> tuple[list[str], list[tuple[int, str]]]:
            #print(post_link)
            nonlocal base_wait_time, wait_time
            post_images: list[str] = []
            failed_urls: list[tuple[int, str]] = []
            streak = 0
            found = 0
            for i in range(max_retries):
                media_found = False
                try:
                    soup = self.soupify(post_link, delay=base_wait_time, xpath="//article")
                    content: bs4.element.Tag  = soup.find("article")
                    content = content.find("div").find("div")
                    contents = content.find_all("div", recursive=False)
                    if len(contents) < 3:
                        vid = content.find("video")
                        link = vid.get("src")
                        post_images.append(link)
                        found += 1
                        media_found = True
                        break
                    content = contents[2]
                    content = content.find_all("div", recursive=False)[1]
                    anchors = content.find_all("a")
                    anchor: bs4.element.Tag
                    for anchor in anchors:
                        img = anchor.find("img")
                        if img is not None:
                            link = img.get("src")
                            link = link.split("&name=")[0]
                            link = f"{link}&name=4096x4096" # Get the highest resolution image
                            post_images.append(link)
                            found += 1
                            media_found = True
                            break
                        vid = anchor.find("video")
                        if vid is not None:
                            post_images.append(vid.get("src"))
                            found += 1
                            media_found = True
                            break
                        else:
                            raise Exception(f"No image or video found: {post_link} > {anchor}")
                    break
                except:
                    streak = 0
                    wait_boost = 0
                    if i == max_retries - 1:
                        print(f"Failed to get media: {post_link}")
                        if log_failure:
                            self.log_failed_url(post_link)
                        failed_urls.append((found, post_link))
                        continue

                    if i == max_retries - 2:
                        #base_wait_time = wait_time
                        wait_boost = 300 # Wait 5 minutes before retrying
                        # Arbitrary, but should be enough time to get around rate limiting
                    wait_time = min(base_wait_time * (2 ** i), max_wait_time)
                    jitter = random.uniform(0, wait_time)
                    wait_time += jitter + wait_boost
                    print(f"Attempt {i+1} failed. Retrying in {wait_time:.2f} seconds...")
                    sleep(wait_time)

                if media_found:
                    if i == 0:
                        streak += 1
                        if streak == 3:
                            pass
                            #base_wait_time *= 0.9 # Reduce wait time if successful on first try
                            #base_wait_time = max(base_wait_time, true_base_wait_time)
                    break

            return post_images, failed_urls

        cookie_value = Config.config.cookies["Twitter"]
        self.driver.add_cookie({"name": "auth_token", "value": cookie_value})
        dir_name = self.current_url.split("/")[3]
        if "/media" not in self.current_url:
            self.current_url = f"https://twitter.com/{dir_name}/media"
        
        sleep(wait_time)
        post_links = {} # Acts as an ordered set # Python 3.7+
        new_posts = True
        while new_posts:
            new_posts = False
            soup = self.soupify()
            rows = soup.find("section").find("div").find("div").find_all("div", recursive=False)
            row: bs4.element.Tag
            for row in rows:
                posts = row.find_all("li")
                post: bs4.element.Tag
                for post in posts:
                    post_link = post.find("a")
                    if post_link is None:
                        continue
                    post_link = post_link.get("href")
                    if post_link not in post_links:
                        post_links[post_link] = None
                        new_posts = True
            if new_posts:
                self.scroll_page()
                sleep(wait_time)
        
        images = []
        failed_urls = []
        num_posts = len(post_links)
        for i, link in enumerate(post_links):
            post_link = f"https://twitter.com{link}"
            print(f"Post {i+1}/{num_posts}: {post_link}")
            links, retry_urls = twitter_parse_helper(post_link, False)
            total_found = len(images)
            images.extend(links)
            failed_urls.extend([(total_found + i, link) for i, link in retry_urls])

        if failed_urls:
            self.driver.delete_all_cookies()
            self.driver.add_cookie({"name": "auth_token", "value": cookie_value})
            sleep(600) # Wait 10 minutes before retrying failed links
            failed = [x for x in failed_urls] # Copy list
            num_failed = len(failed)
            failed_urls = []
            for i, (index, link) in enumerate(failed):
                print(f"Retrying failed post {i+1}/{num_failed}: {link}")
                links, _ = twitter_parse_helper(link, True) # Not retrying failed links
                images[index:index] = links # Insert at index i

        return RipInfo(images, dir_name, self.filename_scheme)

    def wantedbabes_parse(self) -> RipInfo:
        """Parses the html for wantedbabes.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", id="main-content").find("h1").text
        images = soup.find_all("div", class_="gallery")
        images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for im in images for img in im.find_all("img")]
        return RipInfo(images, dir_name, self.filename_scheme)

    def wnacg_parse(self) -> RipInfo:
        """
            Parses the html for wnacg.com and extracts the relevant information necessary for downloading images from the site
        """
        if "-slist-" in self.current_url:
            self.current_url = self.current_url.replace("-slist-", "-index-")
        soup = self.soupify()
        dir_name = soup.find("h2").text
        num_images = soup.find("span", class_="name tb").text # Count number placements e.g. 100 -> 3, 1000 -> 4
        magnitude = len(num_images)
        image_links: list[str] = []
        while True:
            image_list = soup.find_all("li", class_="li tb gallary_item")
            image_list = [img.find("a").get("href") for img in image_list]
            image_links.extend(image_list)
            next_page_button = soup.find("span", class_="next")
            if next_page_button is None:
                break
            next_page_url = next_page_button.find("a").get("href")
            if next_page_url == "":
                raise RipperError("Next page url not found")
            soup = self.soupify(f"https://www.wnacg.com{next_page_url}")

        images = []
        for i, image_link in enumerate(image_links):
            soup = self.soupify(f"https://www.wnacg.com{image_link}")
            img = soup.find("img", id="picarea")
            img_src = img.get("src")
            images.append(img_src if "https:" in img_src else f"https:{img_src}")

        return RipInfo(images, dir_name, self.filename_scheme)

        # while True:
        #     image_list = soup.find_all("li", class_="li tb gallary_item")
        #     image_list = [img.find("img").get("src") for img in image_list]
        #     image_links.extend(image_list)
        #     next_page_button = soup.find("span", class_="next")
        #     if next_page_button is None:
        #         break
        #     next_page_url = next_page_button.find("a").get("href")
        #     if next_page_url == "":
        #         raise RipperError("Next page url not found")
        #     soup = self.soupify(f"https://www.wnacg.com{next_page_url}")

        # images = []
        # for i, image in enumerate(image_links):
        #     ext = image.split(".")[-1]
        #     url = f"https:{image}"
        #     url = url.replace("t4.", "img5.").replace("/t/", "/")
        #     if num_images.isnumeric():
        #         url = "/".join(url.split("/")[:-1])
        #         match magnitude:
        #             case 1:
        #                 url += f"/{i + 1:01d}.{ext}"
        #             case 2:
        #                 url += f"/{i + 1:02d}.{ext}"
        #             case 3:
        #                 url += f"/{i + 1:03d}.{ext}"
        #             case _:
        #                 raise RipperError("Invalid number of digits in number of images")
        #     images.append(url)
        # return RipInfo(images, dir_name, self.filename_scheme)

    # TODO: Work on saving self.driver across sites to avoid relogging in
    def v2ph_parse(self) -> RipInfo:
        """Parses the html for v2ph.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        global logged_in
        try:
            cookies = pickle.load(open("cookies.pkl", "rb"))
            logged_in = True
        except IOError:
            cookies = []
            logged_in = False
        for cookie in cookies:
            self.driver.add_cookie(cookie)
        LAZY_LOAD_ARGS = (True, 1250, 0.75)
        self.lazy_load(*LAZY_LOAD_ARGS)
        soup = self.soupify()
        dir_name = soup.find("h1", class_="h5 text-center mb-3").text
        num = soup.find("dl", class_="row mb-0").find_all("dd")[-1].text
        digits = ("0", "1", "2", "3", "4", "5", "6", "7", "8", "9")
        for i, d in enumerate(num):
            if d not in digits:
                num = num[:i]
                break
        num_pages = int(num)
        base_url = self.current_url
        base_url = base_url.split("?")[0]
        images = []
        parse_complete = False
        for i in range(num_pages):
            if i != 0:
                next_page = "".join([base_url, "?page=", str(i + 1)])
                self.current_url = next_page
                if not logged_in:
                    curr_page = self.current_url
                    self.current_url = "https://www.v2ph.com/login?hl=en"
                    while self.current_url == "https://www.v2ph.com/login?hl=en":
                        sleep(0.1)
                    pickle.dump(self.driver.get_cookies(), open("cookies.pkl", "wb"))
                    self.current_url = curr_page
                    logged_in = True
                self.lazy_load(*LAZY_LOAD_ARGS)
                soup = self.soupify()
            while True:
                image_list = soup.find("div", class_="photos-list text-center").find_all("div",
                                                                                         class_="album-photo my-2")
                if len(image_list) == 0:
                    parse_complete = True
                    break
                image_list = [img.find("img").get("src") for img in image_list]
                if not any([img for img in image_list if "data:image/gif;base64" in img]):
                    break
                else:
                    self.driver.find_element(By.TAG_NAME, 'body').send_keys(Keys.CONTROL + Keys.HOME)
                    self.lazy_load(*LAZY_LOAD_ARGS)
                    soup = self.soupify()
            images.extend(image_list)
            if parse_complete:
                break
        return RipInfo(images, dir_name, self.filename_scheme)

    def xarthunter_parse(self) -> RipInfo:
        """Parses the html for xarthunter.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        return self.__generic_html_parser_1()

    def xmissy_parse(self) -> RipInfo:
        """Parses the html for xmissy.nl and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", id="pagetitle").text
        images = soup.find("div", id="gallery").find_all("div", class_="noclick-image")
        images = [
            img.find("img").get("data-src") if img.find("img").get("data-src") is not None else img.find("img").get(
                "src")
            for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

     */
    
    private async Task<RipInfo> WnacgParse()
    {
        if (CurrentUrl.Contains("-slist-"))
        {
            CurrentUrl = CurrentUrl.Replace("-slist-", "-index-");
        }
        
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h2").InnerText;
        var numImages = soup.SelectSingleNode("//span[@class='name tb']").InnerText;
        var magnitude = numImages.Length;
        var imageLinks = new List<string>();

        while (true)
        {
            var imageList = soup
                           .SelectNodes("//li[@class='li tb gallary_item']")
                           .Select(n => n.SelectSingleNode(".//img"))
                           .GetSrcs();
            imageLinks.AddRange(imageList);
            var nextPageButton = soup.SelectSingleNode("//span[@class='next']");
            if (nextPageButton is null)
            {
                break;
            }
            
            var nextPageUrl = nextPageButton.SelectSingleNode(".//a").GetAttributeValue("href", "");
            if (nextPageUrl == "")
            {
                throw new RipperException("Next page url not found");
            }
            
            soup = await Soupify($"https://www.wnacg.com{nextPageUrl}");
        }
        
        var images = new List<StringImageLinkWrapper>();
        foreach (var (i, image) in imageLinks.Enumerate())
        {
            var ext = image.Split(".")[^1];
            var url = $"https:{image}";
            url = url.Replace("t4.", "img5.").Replace("/t/", "/");
            url = "/".Join(url.Split("/")[..^1]);
            url += magnitude switch
            {
                1 => $"/{i + 1:D1}.{ext}",
                2 => $"/{i + 1:D2}.{ext}",
                3 => $"/{i + 1:D3}.{ext}",
                _ => throw new Exception("Invalid number of digits in number of images")
            };
            images.Add(url);
        }
        
        return new RipInfo(images, dirName, FilenameScheme);
    }

    #endregion

    private static string ExtractJsonObject(string json)
    {
        var depth = 0;
        var escaped = false;
        var inString = false;
        foreach (var (i, c) in json.Enumerate())
        {
            if (escaped)
            {
                escaped = false;
            }
            else
            {
                switch (c)
                {
                    case '\\':
                        escaped = true;
                        break;
                    case '{':
                        if (!inString)
                        {
                            depth++;
                        }
                        break;
                    case '}':
                        if (!inString)
                        {
                            depth--;
                        }
                        break;
                    case '"':
                        inString = !inString;
                        break;
                }
            }
                
            if (depth == 0)
            {
                return json[..(i + 1)];
            }
        }

        throw new RipperException($"Improperly formatted json: {json}");
    }

    private static async Task<HtmlNode> Soupify(string? url = null, int delay = 0, LazyLoadArgs? lazyLoadArgs = null,
                                         HttpResponseMessage? response = null, string xpath = "")
    {
        if (response is not null)
        {
            var content = await response.Content.ReadAsStringAsync();
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(content);
            return htmlDocument.DocumentNode;
        }

        if (url is not null)
        {
            CurrentUrl = url;
        }

        if (delay > 0)
        {
            await Task.Delay(delay);
        }

        if (xpath != "")
        {
            await WaitForElement(xpath);
        }

        if (lazyLoadArgs is not null)
        {
            await LazyLoad(lazyLoadArgs);
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(Driver.PageSource);
        return doc.DocumentNode;
    }

    /// <summary>
    ///     Wait for an element to exist on the page
    /// </summary>
    /// <param name="xpath">XPath of the element to wait for</param>
    /// <param name="delay">Delay between each check</param>
    /// <param name="timeout">Timeout for the wait (-1 for no timeout)</param>
    /// <returns>True if the element exists, false if the timeout is reached</returns>
    private static async Task<bool> WaitForElement(string xpath, float delay = 0.1f, float timeout = 10)
    {
        var timeoutSpan = TimeSpan.FromSeconds(timeout);
        var startTime = DateTime.Now;
        while (Driver.FindElements(By.XPath(xpath)).Count == 0)
        {
            await Task.Delay((int)(delay * 1000));
            var currTime = DateTime.Now;
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (timeout != -1 && currTime - startTime >= timeoutSpan)
            {
                return false;
            }
        }

        return true;
    }
    
    private static void CleanTabs(string urlMatch)
    {
        var windowHandles = Driver.WindowHandles;
        foreach (var handle in windowHandles)
        {
            Driver.SwitchTo().Window(handle);
            if (!CurrentUrl.Contains(urlMatch))
            {
                Driver.Close();
            }
        }

        Driver.SwitchTo().Window(Driver.WindowHandles[0]);
    }

    private static Dictionary<string, List<string>> CreateExternalLinkDict()
    {
        var externalLinks = new Dictionary<string, List<string>>();
        foreach (var site in ExternalSites)
        {
            externalLinks[site] = [];
        }

        return externalLinks;
    }

    private static Dictionary<string, List<string>> ExtractExternalUrls(List<string> urls)
    {
        var externalLinks = CreateExternalLinkDict();
        foreach (var site in externalLinks.Keys)
        {
            foreach (var link in urls.Where(url => !string.IsNullOrEmpty(url) && url.Contains(site))
                                     .Select(UrlUtility.ExtractUrl)
                                     .Where(link => link != ""))
            {
                externalLinks[site].Add(link + '\n');
            }
        }

        return externalLinks;
    }

    private static Dictionary<string, List<string>> ExtractPossibleExternalUrls(List<string> possibleUrls)
    {
        var externalLinks = CreateExternalLinkDict();
        foreach (var site in externalLinks.Keys)
        {
            foreach (var text in possibleUrls)
            {
                if (!text.Contains(site))
                {
                    continue;
                }

                var parts = text.Split();
                foreach (var part in parts)
                {
                    if (!part.Contains(site))
                    {
                        continue;
                    }

                    var link = Utility.UrlUtility.ExtractUrl(part);
                    if (link != "")
                    {
                        externalLinks[site].Add(link + '\n');
                    }
                }
            }
        }

        return externalLinks;
    }

    private static void SaveExternalLinks(Dictionary<string, List<string>> links)
    {
        foreach (var (site, siteLinks) in links)
        {
            if (siteLinks.Count == 0)
            {
                continue;
            }

            File.AppendAllLines($"{site}_links.txt", siteLinks);
        }
    }

    private static async Task<List<string>> ExtractDownloadableLinks(Dictionary<string, List<string>> srcDict,
                                                                     Dictionary<string, List<string>> dstDict)
    {
        var downloadableLinks = new List<string>();
        var downloadableSites = new[] { "sendvid.com" };
        foreach (var site in srcDict.Keys)
        {
            if (downloadableSites.Contains(site))
            {
                downloadableLinks.AddRange(srcDict[site]);
                srcDict[site].Clear();
            }
            else
            {
                dstDict[site].AddRange(srcDict[site]);
            }
        }

        return await ResolveDownloadableLinks(downloadableLinks);
    }

    private static async Task<List<string>> ResolveDownloadableLinks(List<string> links)
    {
        var resolvedLinks = new List<string>();
        var client = new HttpClient();
        foreach (var link in links)
        {
            if (link.Contains("sendvid.com"))
            {
                var response = await client.GetAsync(link);
                var content = await response.Content.ReadAsStringAsync();
                var soup = new HtmlDocument();
                soup.LoadHtml(content);
                var sourceLink = soup.DocumentNode.SelectSingleNode("//source[@id='video_source']")
                                     .GetAttributeValue("src", "");
                resolvedLinks.Add(sourceLink);
            }
            else
            {
                resolvedLinks.Add(link);
            }
        }

        return resolvedLinks;
    }

    /// <summary>
    ///     Scrolls through the page to lazy load images
    /// </summary>
    /// <param name="args">Arguments for lazy loading</param>
    private static Task LazyLoad(LazyLoadArgs args)
    {
        return LazyLoad(args.ScrollBy, args.Increment, args.ScrollPauseTime, args.ScrollBack, args.ReScroll);
    }
    
    /// <summary>
    ///     Scroll through the page to lazy load images
    /// </summary>
    /// <param name="scrollBy">Whether to scroll through the page or instantly scroll to the bottom</param>
    /// <param name="increment">Distance to scroll by each iteration</param>
    /// <param name="scrollPauseTime">Seconds to wait between each scroll</param>
    /// <param name="scrollBack">Distance to scroll back by after reaching the bottom of the page</param>
    /// <param name="rescroll">Whether scrolling through the page again</param>
    private static async Task LazyLoad(bool scrollBy = false, int increment = 2500, int scrollPauseTime = 500,
                                       int scrollBack = 0, bool rescroll = false)
    {
        var lastHeight = (long)Driver.ExecuteScript("return window.pageYOffset");
        if (rescroll)
        {
            Driver.ExecuteScript("window.scrollTo(0, 0);");
        }

        string scrollScript;
        string heightCheckScript;
        if (scrollBy)
        {
            scrollScript = $"window.scrollBy({{top: {increment}, left: 0, behavior: 'smooth'}});";
            heightCheckScript = "return window.pageYOffset";
        }
        else
        {
            scrollScript = "window.scrollTo(0, document.body.scrollHeight);";
            heightCheckScript = "return document.body.scrollHeight";
        }

        while (true)
        {
            Driver.ExecuteScript(scrollScript);
            await Task.Delay(scrollPauseTime);
            var newHeight = Convert.ToInt64(Driver.ExecuteScript(heightCheckScript));
            if (newHeight == lastHeight)
            {
                if (scrollBack > 0)
                {
                    for (var i = 0; i < scrollBack; i++)
                    {
                        Driver.ExecuteScript($"window.scrollBy({{top: {-increment}, left: 0, behavior: 'smooth'}});");
                        await Task.Delay(scrollPauseTime);
                    }

                    await Task.Delay(scrollPauseTime);
                }

                break;
            }

            lastHeight = newHeight;
        }
        //Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10); // TODO: Check if this is necessary
    }

    private static void Print(object? value)
    {
        Console.WriteLine(value);
    }

    private static async Task PrintAsync(string? value, bool error = false)
    {
        if (error)
        {
            await Console.Error.WriteLineAsync(value);
        }
        else
        {
            await Console.Out.WriteLineAsync(value);
        }
    }

    #region Parser Testing

    public async Task<RipInfo> TestParse(string givenUrl, bool debug, bool printSite)
    {
        try
        {
            var options = InitializeOptions(debug ? "debug" : "");
            Driver = new FirefoxDriver(options);
            CurrentUrl = givenUrl.Replace("members.", "www.");
            SiteName = TestSiteCheck(givenUrl);

            Print($"Testing: {SiteName}Parse");
            var start = DateTime.Now;
            var data = await EvaluateParser(SiteName);
            var end = DateTime.Now;
            Print(data.Urls[0].Referer);
            Print($"Time Elapsed: {end - start}");
            var outData = data.Urls.Select(d => d.Url).ToList();
            JsonUtility.Serialize("test.json", outData);
            if (debug)
            {
                Print("Press any key to exit...");
                Console.ReadKey();
            }

            return data;
        }
        catch
        {
            await File.WriteAllTextAsync("test.html", Driver.PageSource);
            //var screenshot = Driver.GetFullPageScreenshot();
            var screenshot = Driver.GetScreenshot();
            screenshot.SaveAsFile("test.png");
            throw;
        }
        finally
        {
            if (printSite)
            {
                await File.WriteAllTextAsync("test.html", Driver.PageSource);
            }

            Driver.Quit();
        }
    }

    private Task<RipInfo> EvaluateParser(string siteName)
    {
        siteName = TestSiteConverter(siteName);
        siteName = siteName[0].ToString().ToUpper() + siteName[1..];
        var methodName = $"{siteName}Parse";
        var method = GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method != null)
        {
            // The second parameter is null because the method has no parameters
            return (Task<RipInfo>)(method.Invoke(this, null) ?? new InvalidOperationException());
        }

        var methods = typeof(HtmlParser).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance);
        var normalizedName = siteName.ToLower();
        foreach (var m in methods)
        {
            if (m.Name.Contains(normalizedName, StringComparison.CurrentCultureIgnoreCase))
            {
                return (Task<RipInfo>)(m.Invoke(this, null) ??
                                       throw new InvalidOperationException()); // The second parameter is null because the method has no parameters
            }
        }

        // Handle the case where the method does not exist
        Print($"Method {methodName} not found.");
        throw new InvalidOperationException();
    }

    private static string TestSiteConverter(string siteName)
    {
        if (siteName.Contains("100bucksbabes"))
        {
            siteName = siteName.Replace("100bucksbabes", "HundredBucksBabes");
        }

        if (siteName.Contains("chapmanganato"))
        {
            siteName = siteName.Replace("chapmanganato", "Manganato");
        }
        
        if (siteName[0] >= '0' && siteName[0] <= '9')
        {
            siteName = NumberToWord(siteName[0]) + siteName[1..];
        }

        return siteName.Remove("-");
    }

    private static string NumberToWord(char number)
    {
        return number switch
        {
            '0' => "zero",
            '1' => "one",
            '2' => "two",
            '3' => "three",
            '4' => "four",
            '5' => "five",
            '6' => "six",
            '7' => "seven",
            '8' => "eight",
            '9' => "nine",
            _ => throw new RipperException("Invalid number")
        };
    }

    private string TestSiteCheck(string url)
    {
        var domain = new Uri(url).Host;
        RequestHeaders["referer"] = $"https://{domain}/";
        domain = DomainNameOverride(domain);
        if (url.Contains("https://members.hanime.tv/") || url.Contains("https://hanime.tv/"))
        {
            RequestHeaders["referer"] = "https://cdn.discordapp.com/";
        }
        else if (url.Contains("https://kemono.party/"))
        {
            RequestHeaders["referer"] = "";
        }

        return domain;
    }

    private static string DomainNameOverride(string url)
    {
        string[] specialDomains = ["inven.co.kr", "danbooru.donmai.us"];
        var urlSplit = url.Split(".");
        return specialDomains.Any(url.Contains) ? urlSplit[^3] : urlSplit[^2];
    }
    
    public static void CloseDriver()
    {
        Driver.Quit();
    }

    [GeneratedRegex("(tags=[^&]+)")]
    private static partial Regex Rule34Regex();
    [GeneratedRegex(@"(/p=\d+)")]
    private static partial Regex HentaiCosplayRegex();
    [GeneratedRegex(@"t\d\.")]
    private static partial Regex NHentaiRegex();
    [GeneratedRegex(@"swf: ?""([^""]+)""")]
    private static partial Regex NewgroundsRegex();
    [GeneratedRegex(@"id=(\d+)")]
    private static partial Regex NijieRegex();
    [GeneratedRegex(@"-\d+x\d+")]
    private static partial Regex SxChineseGirlzRegex();

    #endregion
}