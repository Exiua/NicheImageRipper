using System.Collections.Frozen;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Core.Configuration;
using Core.DataStructures;
using Core.DataStructures.VideoCapturers;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.Managers;
using Core.SiteParsing.HtmlParserEnums;
using Core.Utility;
using FlareSolverrIntegration.Responses;
using HtmlAgilityPack;
using OpenQA.Selenium;
using OpenQA.Selenium.BiDi;
using OpenQA.Selenium.Firefox;
using Serilog;
using Serilog.Events;
using Cookie = OpenQA.Selenium.Cookie;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing;

public partial class HtmlParser : IDisposable
{
    private const string Protocol = "https:";

    private static readonly string[] ExternalSites =
        ["drive.google.com", "mega.nz", "mediafire.com", "sendvid.com", "dropbox.com"];

    private static readonly string[] ParsableSites = ["drive.google.com", "mega.nz", "sendvid.com", "dropbox.com"];
    
    private static Config Config => Config.Instance;
    
    //public static Dictionary<string, bool> SiteLoginStatus { get; set; } = new();
    
    private WebDriver WebDriver { get; set; }
    public bool Interrupted { get; set; }
    private string SiteName { get; set; }
    public float SleepTime { get; set; }
    public float Jitter { get; set; }
    private string GivenUrl { get; set; }
    private FilenameScheme FilenameScheme { get; set; }
    private Dictionary<string, string> RequestHeaders { get; set; }

    private FirefoxDriver Driver => WebDriver.Driver;
    private string CurrentUrl
    {
        get => Driver.Url;
        set => Driver.Url = value;
    }
    private static bool Debugging { get; set; }
    private static FlareSolverrManager FlareSolverrManager => NicheImageRipper.FlareSolverrManager;
    private static string UserAgent => Config.UserAgent;

    public HtmlParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "",
                      FilenameScheme filenameScheme = FilenameScheme.Original)
    {
        //var options = InitializeOptions(siteName);
        //Driver = new FirefoxDriver(options);
        WebDriver = driver;
        RequestHeaders = requestHeaders;
        FilenameScheme = filenameScheme;
        Interrupted = false;
        SiteName = siteName;
        SleepTime = 0.2f;
        Jitter = 0.5f;
        GivenUrl = "";
    }

    public void SetDebugMode(bool debug)
    {
        /*Driver.Quit();
        Driver = new FirefoxDriver(InitializeOptions(debug));*/
        WebDriver.RegenerateDriver(debug);
        Debugging = debug;
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
        catch(Exception e)
        {
            Log.Error(e, "Failed to parse {CurrentUrl}", CurrentUrl);
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
            "thothub" => ThothubParse,
            "titsintops" => TitsInTopsParse,
            "toonily" => ToonilyParse,
            "tsumino" => TsuminoParse,
            "twitter" => TwitterParse,
            "x" => TwitterParse,
            "wantedbabes" => WantedBabesParse,
            "xarthunter" => XArtHunterParse,
            "xmissy" => XMissyParse,
            "yande" => YandeParse,
            "18kami" => EighteenKamiParse,
            "cup2d" => Cup2DParse,
            "5ge" => FiveGeParse,
            "japaneseasmr" => JapaneseAsmrParse,
            "spacemiss" => SpaceMissParse,
            "xiuren" => XiurenParse,
            "xchina" => XChinaParse,
            "gofile" => GoFileParse,
            "jpg5" => Jpg5Parse,
            "simpcity" => SimpCityParse,
            "rule34video" => Rule34VideoParse,
            "av19a" => Av19aParse,
            "eporner" => EpornerParse,
            "cgcosplay" => CgCosplayParse,
            "4khd" => FourKHdParse,
            "cosplay69" => Cosplay69Parse,
            "nlegs" => NLegsParse,
            "ladylap" => LadyLapParse,
            "xasiat" => XasiatParse,
            "catbox" => CatBoxParse,
            "jrants" => JRantsParse,
            "sexbjcam" => SexBjCamParser,
            "pornavhd" => PornAvHdParse,
            "knit" => KnitParse,
            "69tang" => Six9TangParse,
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

    public static FirefoxOptions InitializeOptions(bool debug)
    {
        var options = new FirefoxOptions
        {
            UseWebSocketUrl = true
        };
        
        if (!debug)
        {
            options.AddArgument("--headless");
        }

        options.SetPreference("general.useragent.override", UserAgent);
        options.SetPreference("media.volume_scale", "0.0");
        
        return options;
    }

    private Task<bool> SiteLogin()
    {
        Log.Debug("Checking if already logged in to {SiteName}", SiteName);
        if (IsLoggedInToSite(SiteName))
        {
            Log.Debug("Already logged in to {SiteName}", SiteName);
            return Task.FromResult(true);
        }

        Log.Debug("Logging in to {SiteName}", SiteName);
        var loginTask = SiteName switch
        {
            "nijie" => NijieLogin(),
            "titsintops" => TitsInTopsLogin(),
            "gofile" => GoFileLogin(),
            _ => throw new Exception("Site authentication not implemented")
        };
        
        return loginTask.ContinueWith(task =>
        {
            WebDriver.SiteLoginStatus[SiteName] = task.Result;
            Log.Debug("Logged in to {SiteName}: {Result}", SiteName, task.Result);
            return task.Result;
        });
    }

    private bool IsLoggedInToSite(string siteName)
    {
        var siteLoginStatus = WebDriver.SiteLoginStatus;
        return !siteLoginStatus.TryAdd(siteName, false) && siteLoginStatus[siteName];
    }

    private async Task<bool> NijieLogin()
    {
        var origUrl = CurrentUrl;
        var (username, password) = Config.Logins["Nijie"];
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

    private async Task<bool> TitsInTopsLogin()
    {
        var origUrl = CurrentUrl;
        var (username, password) = Config.Logins["TitsInTops"];
        CurrentUrl = "https://titsintops.com/phpBB2/index.php?login/login";
        var loginInput = Driver.TryFindElement(By.XPath("//input[@name='login']"));
        while (loginInput is null)
        {
            await Task.Delay(100);
            loginInput = Driver.TryFindElement(By.XPath("//input[@name='login']"));
        }
        loginInput.SendKeys(username);
        var passwordInput = Driver.FindElement(By.XPath("//input[@name='password']"));
        passwordInput.SendKeys(password);
        Driver.FindElement(By.XPath("//button[@class='button--primary button button--icon button--icon--login']")).Click();
        while (Driver.TryFindElement(By.XPath("//button[@class='button--primary button button--icon button--icon--login']")) is not null)
        {
            await Task.Delay(100);
        }
        
        CurrentUrl = origUrl;
        return true;
    }

    private async Task<bool> GoFileLogin()
    {
        var origUrl = CurrentUrl;
        var loginLink = Config.Custom[ConfigKeys.CustomKeys.GoFile]["loginLink"];
        CurrentUrl = loginLink;
        await Task.Delay(10000);
        for (var i = 0; i < 4; i++)
        {
            await Task.Delay(2500);
            if (CurrentUrl == "https://gofile.io/myProfile")
            {
                Log.Debug("Logged in to GoFile");
                break;
            }
            
            if (i == 3)
            {
                Log.Warning("Failed to login to GoFile: {CurrentUrl}", CurrentUrl);
                Driver.GetScreenshot().SaveAsFile("test2.png");
            }
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
        var baseUrl = CurrentUrl;
        var urlSplit = baseUrl.Split("/");
        var sourceSite = urlSplit[3];
        baseUrl = string.Join("/", urlSplit[3..6]).Split("?")[0];
        baseUrl = $"{domainUrl}/api/v1/{baseUrl}";
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@id='user-header__info-top']")
                          .SelectSingleNode("//span[@itemprop='name']").InnerText;
        dirName = $"{dirName} - ({sourceSite})";

        #region Get All Posts

        var client = new HttpClient();
        var posts = new List<JsonObject>();
        var page = 0;
        while (true)
        {
            var response = await client.GetAsync($"{baseUrl}?o={page*50}");
            page++;
            if (!response.IsSuccessStatusCode)
            {
                throw new RipperException($"Failed to get page {page}");
            }
            
            var json = await response.Content.ReadFromJsonAsync<JsonNode>();
            var jsonPosts = json!.AsArray();
            posts.AddRange(jsonPosts.Select(post => post!.AsObject()));
            if (jsonPosts.Count < 50)
            {
                break;
            }

            await Task.Delay(250);
        }

        #endregion

        #region Parse All Posts

        var images = new List<StringImageLinkWrapper>();
        var externalLinks = CreateExternalLinkDict();
        var numPosts = posts.Count;
        string[] attachmentExtensions =
            [".zip", ".rar", ".mp4", ".webm", ".psd", ".clip", ".m4v", ".7z", ".jpg", ".png", ".webp"];

        foreach (var (i, post) in posts.Enumerate())
        {
            Log.Information("Parsing post {PostNum} of {TotalPosts}", i + 1, numPosts);
            var id = post["id"]!.Deserialize<string>()!;
            Log.Debug("Post ID: {PostId}", id);
            var content = post["content"]!.Deserialize<string>()!;
            soup = await Soupify(content, urlString: false);
            var links = soup.SelectNodesSafe("//a").GetHrefs();
            var possibleLinks = new List<string>();
            var possibleLinksP = soup.SelectNodes("//p");
            if (possibleLinksP is not null)
            {
                possibleLinks.AddRange(possibleLinksP.Select(p => p.InnerText));
            }
            
            var possibleLinksDiv = soup.SelectNodes("//div");
            if(possibleLinksDiv is not null)
            {
                possibleLinks.AddRange(possibleLinksDiv.Select(d => d.InnerText));
            }
            
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

            
            var file = post["file"]!.AsObject();
            var name = file["name"]!.Deserialize<string>()!;
            var path = file["path"]?.Deserialize<string>();
            if(path is not null)
            {
                if (path[0] == '/')
                {
                    path = domainUrl + path;
                }
    
                var imageLink = new ImageLink(path, FilenameScheme, 0, filename: name);
                images.Add(imageLink);
            }
            
            var attachments = post["attachments"]!.AsArray();
            foreach (var attachment in attachments)
            {
                var attachmentName = attachment!["name"]!.Deserialize<string>()!;
                var attachmentPath = attachment["path"]!.Deserialize<string>()!;
                if (attachmentPath[0] == '/')
                {
                    attachmentPath = domainUrl + attachmentPath;
                }
                
                var attachmentLink = new ImageLink(attachmentPath, FilenameScheme, 0, filename: attachmentName);
                images.Add(attachmentLink);
            }
            
            var extractedAttachments = links
                                      .Where(l => attachmentExtensions.Any(l.Contains))
                                      .Select(l => (l.Contains(domainUrl) || l.Contains("http")) ? l : domainUrl + l)
                                      .ToList();


            images.AddRange(extractedAttachments.ToStringImageLinks());
            foreach (var site in ParsableSites)
            {
                images.AddRange(externalLinks[site].ToStringImageLinkWrapperList());
            }

            // var imageListContainer = soup.SelectSingleNode("//div[@class='post__files']");
            // if (imageListContainer is null)
            // {
            //     continue;
            // }
            //
            // var imageList = imageListContainer.SelectNodes("//a[@class='fileThumb image-link']");
            // var imageListLinks = imageList.GetHrefs();
            // images.AddRange(imageListLinks);
        }

        #endregion

        foreach (var site in ExternalSites)
        {
            externalLinks[site] = externalLinks[site].RemoveDuplicates();
        }

        SaveExternalLinks(externalLinks);
        //images = images.RemoveDuplicates(); // Handled in RipInfo.ConvertUrlsToImageLink
        var stringLinks = new List<StringImageLinkWrapper>();
        foreach (var link in images)
        {
            if (!link.Contains("dropbox.com/"))
            {
                stringLinks.Add(link);
            }
            else
            {
                var ripInfo = await DropboxParse(link);
                stringLinks.AddRange(ripInfo.Urls.ToStringImageLinks());
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
            "bustybloom" or "sexyaporno" => GenericHtmlParserHelper1(),
            "elitebabes" => GenericHtmlParserHelper2(),
            "femjoyhunter" or "ftvhunter" or "hegrehunter" or "joymiihub" 
                or "metarthunter" or "pmatehunter" or "xarthunter" => GenericHtmlParserHelper3(),
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

    /// <summary>
    ///     Make requests to booru-like sites and extract image links
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> BooruParse(string siteName, string baseUrl, string pageParameterName, 
                                           int startingPageIndex, int limit, string[]? jsonObjectNavigation = null, 
                                           Dictionary<string, string>? headers = null)
    {
        var tags = Rule34Regex().Match(CurrentUrl).Groups[1].Value;
        tags = Uri.UnescapeDataString(tags);
        var dirName = $"[{siteName}] " + tags.Remove("+").Remove("tags=");
        var session = new HttpClient();
        if (headers is not null)
        {
            foreach (var (key, value) in headers)
            {
                session.DefaultRequestHeaders.Add(key, value);
            }
        }
        var querySeparator = baseUrl[^1] == '&' ? "" : "&";

        var requestUrl = $"{baseUrl}{querySeparator}limit={limit}&{pageParameterName}={startingPageIndex}&{tags}";
        var response = await session.GetAsync(requestUrl);
        JsonNode? json;
        if (!response.IsSuccessStatusCode)
        {
            var solution = await FlareSolverrManager.GetSiteSolution(requestUrl);
            var rawJson = solution.Response;
            var start = rawJson.IndexOf('[');
            var end = rawJson.LastIndexOf(']');
            rawJson = rawJson[start..(end + 1)];
            json = JsonSerializer.Deserialize<JsonNode>(rawJson);
        }
        else
        {
            json = await response.Content.ReadFromJsonAsync<JsonNode>();
        }

        if (json is null)
        {
            throw new RipperException("Failed to deserialize json");
        }
        
        if (jsonObjectNavigation is not null)
        {
            json = jsonObjectNavigation.Aggregate(json, (current, obj) => current[obj]!);
        }
        
        var data = json.AsArray();
        var images = new List<StringImageLinkWrapper>();
        var pid = startingPageIndex + 1;
        while (true)
        {
            var urls = data.Select(post => post!["file_url"]!.Deserialize<string>()!);
            images.AddRange(urls.Select(url => (StringImageLinkWrapper)url));
            if(data.Count < limit)
            {
                break;
            }
            
            response = await session.GetAsync($"{baseUrl}{querySeparator}limit={limit}&{pageParameterName}={pid}&{tags}");
            json = await response.Content.ReadFromJsonAsync<JsonNode>();
            if (jsonObjectNavigation is not null)
            {
                json = jsonObjectNavigation.Aggregate(json, (current, obj) => current![obj]);
            }
            
            data = json!.AsArray();
            pid++;
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    #endregion

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
            var pageCountRaw = metadata.SelectNodes("./div")
                                       .First(div => div.InnerText.Contains("Page"))
                                       .InnerText.Split(" ")[1];
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
            
            return new RipInfo(images, dirName, FilenameScheme, generate: true, numUrls: pageCount);
        }

        if (CurrentUrl.Contains("/hepisode/"))
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
    
    // TODO: Fix this
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
            Log.Information("Page {PageCount} of {Total}", pageCount, total);
            var url = $"https://www.artstation.com/users/{username}/projects.json?page={pageCount}";
            Log.Debug("Requesting {Url}", url);
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
    ///     Parses the html for av19a.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> Av19aParse()
    {
        var soup = await SolveParseAddCookies();
        var dirName = soup.SelectSingleNode("//header[@class='entry-header']").InnerText;
        var player = soup.SelectSingleNode("//div[@id='player']").SelectSingleNode("./iframe");
        var src = player.GetSrc();
        var match = Av19APlaylistIdRegex().Match(src);
        var urlPath = match.Groups[1].Value;
        var filename = match.Groups[2].Value;
        var playlist = $"https://z124fdsf6dsf.onymyway.top/{urlPath}";
        var linkInfo = new ImageLink(playlist, FilenameScheme, 0, filename: $"{filename}.mp4", linkInfo: LinkInfo.M3U8Ffmpeg)
        {
            Referer = "https://david.cdnbuzz.buzz/"
        };

        return new RipInfo([linkInfo], dirName, FilenameScheme);
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
        const int parseDelay = 500;
        if (url != "")
        {
            CurrentUrl = url;
        }
        
        var soup = await Soupify();
        string dirName;
        if (url == "")
        {
            var dirNameNode = soup.SelectSingleNode("//h1[@class='text-[24px] font-bold text-dark dark:text-white']");
            if (dirNameNode is null)
            {
                dirNameNode = soup.SelectSingleNode("//h1[@class='truncate']");
            }
            
            dirName = dirNameNode.InnerText;
        }
        else
        {
            dirName = "internal-use";
        }

        List<StringImageLinkWrapper> images = [];
        if (CurrentUrl.Contains("/a/"))
        {
            var grid = soup.SelectSingleNode("//div[@class='grid-images']");
            if (grid is not null)
            {
                goto GridInitializationEnd;
            }
            
            grid = soup.SelectSingleNode("//div[@class='grid gap-4 grid-cols-repeat [--size:11rem] lg:[--size:14rem] grid-images']");

            GridInitializationEnd:
            var imagePosts = grid.SelectNodes(".//a");
            foreach (var (i, post) in imagePosts.Enumerate())
            {
                var href = post.GetHref();
                Log.Debug("Post {index} of {total}: {href}", i + 1, imagePosts.Count, href);
                soup = await Soupify(href, delay: parseDelay);
                if (Driver.Title == "502 Bad Gateway")
                {
                    Driver.Refresh();
                    soup = await Soupify(href, delay: parseDelay * 2);
                }
                
                if (CurrentUrl.Contains("/i/"))
                {
                    GetImageLink();
                }
                else if(CurrentUrl.Contains("/v/"))
                {
                    await GetVideoLink(soup);
                }
                else if (CurrentUrl.Contains("/d/"))
                {
                    await GetDownloadLink(soup);
                }
                else
                {
                    Log.Warning("Unknown type: {CurrentUrl}", CurrentUrl);
                }
            }
        }
        else if (CurrentUrl.Contains("/i/"))
        {
            GetImageLink();
        }
        else if(CurrentUrl.Contains("/v/"))
        {
            await GetVideoLink(soup);
        }
        else if (CurrentUrl.Contains("/d/"))
        {
            await GetDownloadLink(soup);
        }
        else
        {
            Log.Warning("Unknown type: {CurrentUrl}", CurrentUrl);
        }

        return new RipInfo(images, dirName, FilenameScheme);

        // ReSharper disable once VariableHidesOuterVariable
        async Task GetVideoLink(HtmlNode soup)
        {
            var videoDownloadNode = soup.SelectSingleNode("//a[@id='czmDownloadz']");
            string videoDownload;
            if (videoDownloadNode is not null)
            {
                videoDownload = videoDownloadNode.GetHref();
            }
            else
            {
                videoDownload = soup.SelectSingleNode("//a[@class='btn btn-main btn-lg rounded-full px-6 font-semibold flex-1 ic-download-01 ic-before before:text-lg']")
                                    .GetHref();
            }
            soup = await Soupify(videoDownload, xpath: "//main//video", delay: parseDelay);
            var video = soup.SelectSingleNode("//main//video");
                    
            var downloadButton = soup.SelectSingleNode(
                "//a[@class='btn btn-main btn-lg rounded-full px-6 font-semibold ic-download-01 ic-before before:text-lg']");
            var downloadUrl = downloadButton.GetHref();
            var filename = video is not null ? video.GetSrc().Split("/")[^1] : downloadUrl.Split("/")[^1];
            var videoLink = new ImageLink(downloadUrl, FilenameScheme, 0, filename: filename);
            images.Add(videoLink);
        }

        void GetImageLink()
        {
            var img = soup.SelectSingleNode("//main//img");
            images.Add(img.GetSrc());
        }

        // ReSharper disable once VariableHidesOuterVariable
        async Task GetDownloadLink(HtmlNode soup)
        {
            var downloadLinkNode = soup.SelectSingleNode("//a[@class='text-white inline-flex items-center justify-center rounded-[5px] py-2 px-4 text-center text-base font-bold hover:text-white mb-2']");
            if (downloadLinkNode is null)
            {
                downloadLinkNode = soup.SelectSingleNode("//a[@class='btn btn-main btn-lg rounded-full px-6 font-semibold ic-download-01 ic-before before:text-lg flex-1']");
            }
                    
            var downloadLink = downloadLinkNode.GetHref();
            soup = await Soupify(downloadLink, delay: parseDelay);
            var link = soup.SelectSingleNode("//a[@class='btn btn-main btn-lg rounded-full px-6 font-semibold ic-download-01 ic-before before:text-lg']")
                           .GetHref();
            images.Add(link);
        }
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
    ///     Parses the html for catbox.moe and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> CatBoxParse()
    {
        Log.Warning("Catbox.moe support is experimental and may not work as expected");
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='title']/h1").InnerText;
        var images = soup.SelectSingleNode("//div[@class='imagecontainer']")
                         .SelectNodes("./video")
                         .Select(vid => vid.GetSrc())
                         .ToStringImageLinkWrapperList();

        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    /// <summary>
    ///     Parses the html for cgcosplay.org and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> CgCosplayParse()
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250
        });
        var dirName = soup.SelectSingleNode("//h2[@class='elementor-heading-title elementor-size-xxl']").InnerText;
        var images = soup.SelectSingleNode("//div[@id='gallery-1']")
                         .SelectNodes("./figure")
                         .Select(fig => fig.SelectSingleNode(".//img").GetSrc())
                         .ToStringImageLinkWrapperList();
        var videos = soup.SelectSingleNode("//main[@id='main']");
        if (videos is not null)
        {
            var videoRawLinks = videos.SelectNodes(".//*[self::iframe or self::video]")
                                      // .Select(div =>
                                      //      div.SelectSingleNode(".//video") ?? div.SelectSingleNode(".//iframe"))
                                      .Select(elm => elm.GetSrc());
            var captures = new Dictionary<string, PlaylistCapturer>();
            foreach (var (i, link) in videoRawLinks.Enumerate())
            {
                var cleanLink = link.DecodeUrl();
                Log.Debug("Video {index}: {link}", i + 1, cleanLink);
                if (cleanLink.Contains("cgcosplay.org"))
                {
                    images.Add(cleanLink);
                }
                else if (cleanLink.Contains("vk.com"))
                {
                    if (!captures.TryGetValue("vk.com", out var capturer))
                    {
                        (capturer, _) = await ConfigureNetworkCapture<VkVideoCapturer>();
                        captures.Add("vk.com", capturer);
                    }
                    
                    var resolvedLink = await ResolveVkLink(cleanLink, capturer);
                    images.Add(resolvedLink);
                }
                else if (cleanLink.Contains("youtube.com"))
                {
                    images.Add(cleanLink);
                }
                else if (cleanLink.Contains("late-anxiety.com"))
                {
                    Log.Debug("Suppressed spam link");
                    // suppress, it's just spam
                }
                else
                {
                    Log.Warning("Link not handled: {link}", cleanLink);
                }
            }
        }

        return new RipInfo(images, dirName, FilenameScheme);

        async Task<string> ResolveVkLink(string url, PlaylistCapturer capturer)
        {
            CurrentUrl = url;
            await Task.Delay(250);
            while (true)
            {
                if (CurrentUrl.Contains("autoplay=1"))
                {
                    break;
                }
                
                var playButton = Driver.TryFindElement(By.XPath("//div[@class='videoplayer_thumb']"));
                if (playButton is null)
                {
                    Log.Debug("Play button not found, retrying...");
                    Driver.TakeDebugScreenshot();
                    await Task.Delay(1000);
                    continue;
                }

                playButton.Click();
                break;
            }

            List<string> links;
            while (true)
            {
                links = capturer.GetNewVideoLinks();
                if (links.Count == 0)
                {
                    Log.Debug("No links found, retrying...");
                    await Task.Delay(1000);
                    continue;
                }
                
                break;
            }

            return links[0];
        }
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

    /// <summary>
    ///     Parses the html for cosplay69.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> Cosplay69Parse()
    {
        var soup = await Soupify(/*delay: 5000, */lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250
        });

        var dirName = soup.SelectSingleNode("//h1[@class='post-title entry-title']").InnerText;
        List<StringImageLinkWrapper> images;
        var video = soup.SelectSingleNode("//iframe");
        if (video is not null)
        {
            var (capturer, _) = await ConfigureNetworkCapture<Cosplay69VideoCapturer>();
            Driver.Refresh();
            while (true)
            {
                var links = capturer.GetNewVideoLinks();
                if (links.Count == 0)
                {
                    Log.Debug("No links found, retrying...");
                    await Task.Delay(1000);
                    continue;
                }
                
                images = links.ToStringImageLinkWrapperList();
                break;
            }
        }
        else
        {
            images = soup.SelectSingleNode("//div[@class='entry-content gridnext-clearfix']")
                         .SelectNodes(".//img")
                         .Select(img => img.GetSrc())
                         .ToStringImageLinkWrapperList();
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    /// <summary>
    ///     Parses the html for cup2d.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> Cup2DParse()
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250
        });
        var dirName = soup.SelectSingleNode("//h1[@class='post-title entry-title']/a").InnerText;
        var images = new List<StringImageLinkWrapper>();
        var node = soup.SelectSingleNode("//div[@class='entry-content gridshow-clearfix']/div")
                       .SelectNodes("./*[self::a or self::iframe]");
        foreach (var n in node)
        {
            if(n.Name == "a")
            {
                images.Add((StringImageLinkWrapper)n.GetHref());
            }
            else
            {
                images.Add((StringImageLinkWrapper)n.GetSrc().Replace("/embed/", "/file/"));
            }
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }
    
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

    private async Task<RipInfo> CyberDropParse()
    {
        return await CyberDropParse("");
    }
    
    /// <summary>
    ///     Parses the html for cyberdrop.me and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> CyberDropParse(string url)
    {
        const int parseDelay = 500;
        
        if (url != "")
        {
            CurrentUrl = url;
        }

        var soup = await SolveParseAddCookies();
        var titleNode = soup.SelectSingleNode("//h1[@id='title']");
        var dirName = titleNode is not null ? titleNode.InnerText : $"[CyberDrop] {CurrentUrl.Split("/")[^1]}";
        
        var images = new List<StringImageLinkWrapper>();
        if (CurrentUrl.Contains("/a/"))
        {
            var imageList = soup.SelectNodes("//div[@class='image-container column']")
                                .Select(image => image
                                                .SelectSingleNode(".//a[@class='image']")
                                                .GetHref())
                                .Select(href => $"https://cyberdrop.me{href}");
            foreach (var image in imageList)
            {
                soup = await Soupify(image, delay: parseDelay, xpath: "//a[@id='downloadBtn']");
                var link = soup
                          .SelectSingleNode("//a[@id='downloadBtn']")
                          .GetHref();
                images.Add(link);
            }
        }
        else if (CurrentUrl.Contains("/f/"))
        {
            var link = soup
                      .SelectSingleNode("//a[@id='downloadBtn']")
                      .GetHref();
            images.Add(link);
        }
        else if (CurrentUrl.Contains("/e/"))
        {
            var video = soup.SelectSingleNode("//video[@id='player']");
            if (video is null)
            {
                await Task.Delay(parseDelay);
                soup = await Soupify(delay: parseDelay, xpath: "//video[@id='player']");
                video = soup.SelectSingleNode("//video[@id='player']");
            }
            
            var link = video.GetVideoSrc();
            images.Add(link);
        }
        else
        {
            Log.Error("Unknown CyberDrop url type: {CurrentUrl}", CurrentUrl);
            throw new RipperException("Unknown CyberDrop url type");
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for danbooru.donmai.us and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns></returns>
    private Task<RipInfo> DanbooruParse()
    {
        return BooruParse("Danbooru", "https://danbooru.donmai.us/posts.json?", 
            "page", 0, 200, headers: new Dictionary<string, string>
            {
                ["User-Agent"] = "NicheImageRipper"
            });
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

                Log.Error("Could not find directory name. Unable to continue.");
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

        return new RipInfo(images.ToStringImageLinkWrapperList(), dirName, FilenameScheme, filenames: filenames);
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
            Log.Information("Parsing page {pageCount}", pageCount);
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
                Log.Warning("Timed out. Sleeping for 10 seconds before retrying...");
                await Task.Delay(10000);
                CurrentUrl = nextPage;
            }
            soup = await Soupify();
        }
        
        var images = new List<StringImageLinkWrapper>();
        foreach (var (i, link) in imageLinks.Enumerate())
        {
            Log.Information("Parsing image {i} of {count}", i + 1, imageLinks.Count);
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
    ///     Parses the html for 18kami.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> EighteenKamiParse()
    {
        var url = CurrentUrl.Split("/")[..5].Join("/").Replace("/album/", "/photo/");
        var soup = await Soupify(url, lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250
        });
        var dirName = soup.SelectSingleNode("//div[@class='panel-heading']/div[@class='pull-left']").InnerText;
        var images = soup.SelectSingleNode("//div[@class='row thumb-overlay-albums']")
                         .SelectNodes(".//img")
                         .Select(img => $"https://18kami.com{img.GetSrc()}")
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
    
    /// <summary>
    ///     Parses the html for elitebabes.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> EliteBabesParse()
    {
        return GenericHtmlParser("elitebabes");
    }

    /// <summary>
    ///     Parses the html for eporner.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> EpornerParse()
    {
        const string domainUrl = "https://www.eporner.com";
        
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250,
            ScrollPauseTime = 250
        };
        
        var soup = await Soupify(lazyLoadArgs: lazyLoadArgs);

        string dirName;
        var images = new List<StringImageLinkWrapper>();
        
        if (CurrentUrl.Contains("/profile/"))
        {
            var baseUrl = CurrentUrl.Split("/")[..5].Join("/");
            dirName = soup.SelectSingleNode("//div[@id='pprofiletopinfo']//h1").InnerText;
            var headers = soup.SelectSingleNode("//div[@id='pnavtop']").SelectNodes("./a");
            var profileSections = EpornerProfileSections.None;
            foreach (var header in headers)
            {
                switch (header.InnerText)
                {
                    case "Videos":
                        profileSections |= EpornerProfileSections.Videos;
                        break;
                    case "Pics / GIFs":
                        profileSections |= EpornerProfileSections.Images;
                        break;
                    case "Playlists":
                        profileSections |= EpornerProfileSections.Playlists;
                        break;
                }
            }

            
            var posts = new List<string>();
            
            #region Extract Videos
            
            if (profileSections.HasFlag(EpornerProfileSections.Videos))
            {
                await ExtractFromListView(baseUrl, "uploaded-videos", "streameventsday showAll", posts);
                // if (profileSections.HasFlag(EpornerProfileSections.Playlists))
                // {
                //     await ExtractFromListView(baseUrl, "playlists", "streameventsday showAll", posts);
                // }
                
                foreach (var post in posts)
                {
                    Log.Information("Parsing video: {post}", post);
                    soup = await Soupify(post, lazyLoadArgs: lazyLoadArgs);
                    var downloadLink = ExtractVideoDownloadLink(soup);
                    images.Add(downloadLink);
                }
            }
            
            #endregion

            #region Extract Images

            posts.Clear();
            if (profileSections.HasFlag(EpornerProfileSections.Images))
            {
                await ExtractFromListView(baseUrl, "uploaded-pics", "streameventsday photosgrid showAll", posts);
                foreach (var post in posts)
                {
                    Log.Information("Parsing gallery: {post}", post);
                    soup = await Soupify(post, lazyLoadArgs: lazyLoadArgs);
                    var postImages = soup.SelectSingleNode("//div[@class='photosgrid gallerygrid']")
                                         .SelectNodes("./div")
                                         .Select(div => div.SelectSingleNode(".//img").GetSrc())
                                         .Select(ExtractFullImageLink)
                                         .ToStringImageLinks();
                    images.AddRange(postImages);
                }
            }
            
            #endregion
        
        }
        else if (CurrentUrl.Contains("/video-") || CurrentUrl.Contains("/hd-porn/"))
        {
            dirName = soup.SelectSingleNode("//div[@id='video-info']/h1").InnerText;
            var downloadLink = ExtractVideoDownloadLink(soup);
            images.Add(downloadLink);
        }
        else
        {
            var e = new RipperException("Unknown Eporner URL type");
            Log.Error(e, "Unknown Eporner URL type: {CurrentUrl}", CurrentUrl);
            throw e;
        }
        
        return new RipInfo(images, dirName, FilenameScheme);

        // ReSharper disable once VariableHidesOuterVariable
        string ExtractVideoDownloadLink(HtmlNode soup)
        {
            var downloads = soup.SelectSingleNode("//div[@id='hd-porn-dload']")
                                .SelectNodes(".//span");
            string downloadLink;
            if (downloads.Count > 1)
            {
                var secondLast = downloads[^2];
                if (secondLast.GetAttributeValue("class") == "download-av1")
                {
                    downloadLink = secondLast.SelectSingleNode("./a").GetHref();
                }
                else
                {
                    var last = downloads[^1];
                    downloadLink = last.SelectSingleNode("./a").GetHref();
                }
            }
            else
            {
                var last = downloads[^1];
                downloadLink = last.SelectSingleNode("./a").GetHref();
            }
            
            return $"https://www.eporner.com{downloadLink}";
        }
        
        string ExtractFullImageLink(string imageLink)
        {
            var parts = imageLink.Split("/");
            var baseLink = parts[..^1].Join("/");
            var filename = parts[^1];
            var extension = filename.Split(".")[^1];
            filename = filename.Split("_")[0];
            var link = $"{baseLink}/{filename}.{extension}";
            return link;
        }

        async Task ExtractFromListView(string baseUrl, string section, string divClass, List<string> posts)
        {
            // ReSharper disable once VariableHidesOuterVariable
            var soup = await Soupify($"{baseUrl}/{section}/", lazyLoadArgs: lazyLoadArgs);
            while(true)
            {
                var links = soup.SelectSingleNode($"//div[@class='{divClass}']")
                                .SelectNodes("./div[contains(@class, 'mb')]")
                                .Select(div => domainUrl + div.SelectSingleNode(".//a").GetHref());
                posts.AddRange(links);
                var nextPage = soup.SelectSingleNode("//a[@class='nmnext']");
                if (nextPage is null)
                {
                    break;
                }
                    
                var nextPageUrl = nextPage.GetHref();
                soup = await Soupify($"{domainUrl}{nextPageUrl}");
            }
        }
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
            Log.Information("Parsing page {page}", page);
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
            Log.Information("Parsing post {i} of {total}", i + 1, total);
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
        string dirName;
        List<StringImageLinkWrapper> images;
        if (CurrentUrl.Contains("/gif/"))
        {
            dirName = soup.SelectSingleNode("//h1[@class='mb-0 title']").InnerText;
            var player = soup.SelectSingleNode("//div[@class='video-player gifs']/video/source");
            images = [player.GetSrc()];
        }
        else if (CurrentUrl.Contains("/video/"))
        {
            dirName = soup.SelectSingleNode("//h1[@class='mb-0 title']").InnerText;
            var player = soup.SelectSingleNode("//video[@class='v-player']/source");
            images = [player.GetSrc()];
        }
        else /*if (CurrentUrl.Contains("/a/"))*/
        {
            dirName = soup.SelectSingleNode("//div[@class='head-title']")
                          .SelectSingleNode(".//span")
                          .InnerText;
            images = soup.SelectSingleNode("//div[@class='album-gallery']")
                         .SelectNodes("./a")
                         .Select(link => link.GetAttributeValue("data-src"))
                         .Select(dummy => (StringImageLinkWrapper)dummy)
                         .ToList();
        }
        

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
    ///     Parses the html for femjoyhunter.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> FemJoyHunterParse()
    {
        return GenericHtmlParser("femjoyhunter");
    }

    /// <summary>
    ///     Parses the html for happy.5ge.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> FiveGeParse()
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250,
            ScrollPauseTime = 1000
        });
        var dirName = soup.SelectSingleNode("//h1[@class='joe_detail__title']").InnerText;
        var images = soup.SelectSingleNode("//div[@class='joe_gird']")
                         .SelectNodes(".//img")
                         .Select(img => img.GetSrc())
                         .ToStringImageLinkWrapperList();

        return new RipInfo(images, dirName, FilenameScheme);
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
            Log.Information("Parsing page {pageCount}", pageCount);
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
            Log.Information("Parsing post {i}: {post}", i + 1, post);
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
    ///     Parses the html for 4khd.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> FourKHdParse()
    {
        await LazyLoad(new LazyLoadArgs
        {
            StopElement = By.XPath("//ul[@class='page-links']")
        });
        
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h3").InnerText;
        var numPages = soup.SelectSingleNode("//ul[@class='page-links']")?
                          .SelectNodes("./li")
                          .Count ?? 1;

        var baseUrl = CurrentUrl;
        var images = new List<StringImageLinkWrapper>();
        for (var page = 1; page <= numPages; page++)
        {
            Log.Information("Parsing page {page} of {numPages}", page, numPages);
            var baseElement = soup.SelectSingleNode("//div[@id='basicExample']") ?? soup.SelectSingleNode("//div[@id='basicE']");
            var imgs = baseElement.SelectNodes("./a")
                                  .Select(a => a.GetHref().Split("?")[0])
                                  .ToStringImageLinks();
            images.AddRange(imgs);
            // // The first page is already loaded
            soup = await Soupify($"{baseUrl}/{page + 1}", lazyLoadArgs: new LazyLoadArgs
            {
                StopElement = By.XPath("//ul[@class='page-links']")
            });
        }

        var baseName = images[0].Split("/")[^1];
        var match = Regex.Match(baseName, @"([a-zA-Z0-9-]+)");
        baseName = match.Groups[1].Value;
        images = images.Where(img => img.Contains(baseName)).ToList();

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
    private Task<RipInfo> GelbooruParse()
    {
        return BooruParse("Gelbooru", "https://gelbooru.com/index.php?page=dapi&s=post&q=index&json=1&", 
            "pid", 0, 100, jsonObjectNavigation: ["post"]);
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

    private Task<RipInfo> GoFileParse()
    {
        return GoFileParse("");
    }
    
    /// <summary>
    ///     Parses the html for gofile.io and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> GoFileParse(string url)
    {
        if (url != "")
        {
            CurrentUrl = url;
        }

        await SiteLogin();
        // var cookie = new Cookie("accountToken", cookieValue, ".gofile.io", "/", null, 
        //     true, false, "Lax");
        // Driver.AddCookie(cookie);
        await Task.Delay(5000);
        var soup = await Soupify();
        var password = soup.SelectSingleNode("//input[@type='password']");
        if (password is not null)
        {
            Log.Warning("URL is password protected. Writing url to file...");
            await File.WriteAllTextAsync("password_protected_gofile.txt", CurrentUrl + "\n");
            // TODO: Find a better way to handle password protected files
            return new RipInfo([], "Password Protected", FilenameScheme);
        }
        
        var folderNotFound = soup.SelectSingleNode("//div[@class='alert alert-secondary border border-danger text-white']");
        if (folderNotFound is not null)
        {
            Log.Warning("Folder not found. Writing url to file...");
            // TODO: Find a better way to indicate that data does not exist
            return new RipInfo([], "Folder Not Found", FilenameScheme);
        }
        
        var dirName = soup.SelectSingleNode("//span[@id='filesContentFolderName']").InnerText;
        var images = await GoFileParserHelper("", true);

        return new RipInfo(images, dirName, FilenameScheme);

        // ReSharper disable once VariableHidesOuterVariable
        async Task<List<StringImageLinkWrapper>> GoFileParserHelper(string url, bool topLevel = false)
        {
            if (url != "")
            {
                Log.Debug("Found nested url: {url}", url);
                CurrentUrl = url;
                await Task.Delay(5000);
            }
            
            var playButtons = Driver.FindElements(By.CssSelector(
                ".btn.btn-outline-secondary.btn-sm.p-1.me-1.filesContentOption.filesContentOptionPlay.text-white"));
            if(playButtons.Count > 1) // If there is one file, it will be expanded by default
            {
                foreach (var button in playButtons)
                {
                    Driver.ScrollElementIntoView(button);
                    try
                    {
                        button.Click();
                    }
                    catch (ElementNotInteractableException)
                    {
                        Driver.GetScreenshot().SaveAsFile("test.png");
                        Driver.ExecuteScript("arguments[0].click();", button);
                    }
                    await Task.Delay(125);
                }
            }

            //Driver.WaitUntilElementExists(By.XPath("//div[@id='filesContentTableContent']/div"));
            // ReSharper disable once VariableHidesOuterVariable
            var soup = await Soupify(xpath: "//div[@id='filesContentTableContent']/div[@id]");
            var links = new List<StringImageLinkWrapper>();
            var entries = soup.SelectNodes("//div[@id='filesContentTableContent']/div");
            foreach (var entry in entries)
            {
                var id = entry.GetNullableAttributeValue("id");
                if (id is null)
                {
                    Log.Error("Entry has no id: {entry}", entry.InnerHtml);
                    Log.Error("Current URL: {url}", CurrentUrl);
                    throw new RipperException("Entry has no id");
                }
                
                var anchor = entry.SelectSingleNode(".//a");
                var href = anchor.GetHref();
                if (href.Contains("/d/"))
                {
                    var nestedLinks = await GoFileParserHelper($"https://gofile.io{href}");
                    links.AddRange(nestedLinks);
                }
                else
                {
                    var elm = soup.SelectSingleNode($"//*[@id='elem-{id}']");
                    if (elm is null)
                    {
                        var span = anchor.SelectSingleNode("./span");
                        var filename = span.InnerText;
                        links.Add($"https://cold8.gofile.io/download/web/{id}/{filename}");
                    }
                    else
                    {
                        switch (elm.Name)
                        {
                            case "img":
                            {
                                var src = elm.GetSrc();
                                links.Add(src);
                                break;
                            }
                            case "video":
                            {
                                var source = elm.SelectSingleNode("./source");
                                var src = source.GetSrc();
                                links.Add(src);
                                break;
                            }
                            default:
                            {
                                // TODO: Handle better
                                Log.Warning("Unknown tag: {tag}", elm.Name);
                                break;
                            }
                        }
                    }
                }
            }

            return links;
        }
    }

    private async Task<RipInfo> GoogleParse()
    {
        return await GoogleParse("");
    }

    /// <summary>
    ///     Query the Google Drive API to get file information to download
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
        if (CurrentUrl.Contains("/video/"))
        {
            CurrentUrl = CurrentUrl.Replace("hentai-cosplays.com", "porn-video-xxx.com");
            return await PornVideoXXXParse();
        }
        
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
                images = [ iframeUrl ]; // YouTube video (probably)
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
        var clientId = Config.Keys["Imgur"];
        if (clientId == "")
        {
            Log.Error("Client Id not set");
            Log.Error("Follow to generate Client Id: https://apidocs.imgur.com/#intro");
            Log.Error("Then add Client Id to Imgur in config.json under Keys");
            throw new RipperCredentialException("Client Id Not Set");
        }

        RequestHeaders["Authorization"] = "Client-ID " + clientId;
        var albumHash = CurrentUrl.Split("/")[5];
        var session = new HttpClient();
        var request = RequestHeaders.ToRequest(HttpMethod.Get, $"https://api.imgur.com/3/album/{albumHash}");
        var response = await session.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            Log.Error("Client Id is incorrect");
            throw new RipperCredentialException("Client Id Incorrect");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonNode>();
        var jsonData = json!["data"]!.AsObject();
        var dirName = jsonData["title"]!.Deserialize<string>()!;
        var images = jsonData["images"]!.AsArray().Select(img => img!["link"]!.Deserialize<string>()!).ToList();

        return new RipInfo(images.ToStringImageLinkWrapperList(), dirName, FilenameScheme);
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
    ///     Parses the html for japaneseasmr.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> JapaneseAsmrParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='page-title']").InnerText;
        var images = soup.SelectSingleNode("//div[@class='fotorama__nav__shaft']")
                         .SelectNodes(".//img")
                         .Select(img => img.GetSrc())
                         .ToStringImageLinkWrapperList();
        var megaLinks = soup.SelectSingleNode("//div[@class='download_links']")
                            .SelectNodes(".//a")
                            .Select(a => a.GetHref());
        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (var link in megaLinks)
        {
            // Unable to bypass Cloudflare atm
            // CurrentUrl = link;
            // while (CurrentUrl == link)
            // {
            //     await Task.Delay(1000);
            // }
            
            images.Add($"text:{link}");
        }

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

    private Task<RipInfo> Jpg5Parse()
    {
        return Jpg5Parse("");
    }
    
    /// <summary>
    ///     Parses the html for jpg5.su and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> Jpg5Parse(string url)
    {
        if(url != "")
        {
            CurrentUrl = url;
        }

        var single = false;
        var soup = await Soupify();
        string? dirName;
        if (CurrentUrl.Contains("/a/"))
        {
            dirName = soup.SelectSingleNode("//a[@data-text='album-name']").InnerText;
        }
        else if (CurrentUrl.Contains("/img/"))
        {
            dirName = soup.SelectSingleNode("//a[@data-text='image-title']").InnerText;
            single = true;
        }
        else
        {
            dirName = soup.SelectSingleNode("//div[@class='header']").InnerText;
        }

        var images = new List<StringImageLinkWrapper>();
        if (!single)
        {
            var page = 1;
            while (true)
            {
                Log.Information($"Parsing page {page}");
                page++;
                var error = soup.SelectSingleNode("//h1");
                if (error is not null && error.InnerText.StartsWith("500 I"))
                {
                    await Task.Delay(5000); // Most likely due to rate limiting
                    Driver.Refresh();
                    soup = await Soupify();
                }

                var posts = soup.SelectSingleNode("//div[@class='pad-content-listing']")
                                .SelectNodes("./div")
                                .Select(div => div.SelectSingleNode(".//img").GetSrc().Remove(".md"))
                                .ToStringImageLinks();
                images.AddRange(posts);
                var nextPage = soup.SelectSingleNode("//a[@data-pagination='next']");
                var nextPageUrl = nextPage?.GetNullableHref();
                if (nextPageUrl is null)
                {
                    break;
                }

                nextPageUrl = nextPageUrl.DecodeUrl();
                //Log.Debug("Next page: {nextPageUrl}", nextPageUrl);
                soup = await Soupify(nextPageUrl, xpath: "//div[@class='pad-content-listing']/div");
            }
        }
        else
        {
            var img = soup.SelectSingleNode("//div[@id='image-viewer-container']/img");
            images.Add(img.GetSrc());
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for jrants.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> JRantsParse()
    {
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250
        };
        
        var soup = await Soupify(lazyLoadArgs: lazyLoadArgs);
        var dirName = soup.SelectSingleNode("//h1[@class='entry-title']").InnerText;
        var images = new List<StringImageLinkWrapper>();
        var pageCount = 1;
        var noImagesFound = false;
        while (true)
        {
            Log.Information("Parsing page {pageCount}", pageCount);
            pageCount++;
            var imgs = soup.SelectNodes("//div[@class='inside-article']//p/img")?
                           .Select(img => img.GetSrc())
                           .ToStringImageLinks();
            
            if (imgs is not null)
            {
                images.AddRange(imgs);
            }
            else
            {
                noImagesFound = true;
            }

            var pagination = soup.SelectSingleNode("//div[@class='pgntn-page-pagination-block']")?
                                 .SelectNodes("./a");

            var nextPage = pagination?.FirstOrDefault(a => a.InnerText.StartsWith("Next"));
            if (nextPage is null)
            {
                break;
            }

            // Some albums don't have images on the last page, so if we find no images and there is a next page, we throw an exception
            if (noImagesFound)
            {
                throw new RipperException("No images found");
            }

            soup = await Soupify(nextPage.GetHref(), lazyLoadArgs: lazyLoadArgs);
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    /// <summary>
    ///     Parses the html for kemono.su and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> KemonoParse()
    {
        return await DotPartyParse("https://kemono.su");
    }

    /// <summary>
    ///     Parses the html for xx.knit.bid and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> KnitParse()
    {
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250,
            ScrollPauseTime = 1000
        };
        
        var agreeButton = Driver.TryFindElement(By.XPath("//button[@id='agree-over18']"));
        if (agreeButton is not null)
        {
            Driver.Click(agreeButton);
        }
        
        var retry = 0;
        while (true)
        {
            await LazyLoad(lazyLoadArgs);
            var scrollHeight = Driver.GetScrollHeight();
            if (scrollHeight <= 650)
            {
                Log.Debug("Page reset");
                CleanTabs("xx.knit.bid");
                retry++;
                if (retry > 4)
                {
                    throw new RipperException("Page reset too many times");
                }
                continue;
            }
            
            var loadMoreButton = Driver.TryFindElement(By.XPath("//div[@class='ias_trigger']"));
            if (loadMoreButton is null)
            {
                Log.Debug("No more images to load");
                break;
            }
            
            loadMoreButton.Click();
        }

        var soup  = await Soupify();    
        var dirName = soup.SelectSingleNode("//h1[@class='focusbox-title']").InnerText;
        var images = soup.SelectSingleNode("//article[@id='img-box']")
            .SelectNodes("./p")
            .Select(p => "https://xx-media.knit.bid" + p.SelectSingleNode("./img").GetSrc())
            .ToStringImageLinkWrapperList();
        var videos = soup.SelectSingleNode("//article[@id='img-box']/div[@class='wrapper']")
            .SelectNodesSafe(".//source")
            .Select(source => source.GetSrc())
            .ToStringImageLinks();
        images.AddRange(videos);

        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    /// <summary>
    ///     Parses the html for ladylap.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> LadyLapParse()
    {
        const string domain = "https://www.ladylap.com";
        const int delay = 1000;

        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//strong").InnerText;
        var numPages = soup.SelectSingleNode("//ul[@class='pagination pagination']")
                           .SelectNodes("./li")
                           .Count;
        var baseUrl = CurrentUrl;
        var images = new List<StringImageLinkWrapper>();
        for (var i = 0; i < numPages; i++)
        {
            Log.Information("Parsing page {i} of {numPages}", i + 1, numPages);
            var posts = soup.SelectNodes("//div[@class='col-md-12 col-lg-12']")[2]
                            .SelectNodes(".//a")
                            .Select(a => domain + a.GetHref())
                            .ToStringImageLinks();
            images.AddRange(posts);
            soup = await Soupify($"{baseUrl}/{i + 2}"); // Pages are 1-indexed
            await Task.Delay(delay);
        }

        var cookieJar = Driver.GetCookieJar();
        var cookies = cookieJar.AllCookies
                               .Aggregate("", (current, cookie) => current + $"{cookie.Name}={cookie.Value}; ")
                               .Trim();
        RequestHeaders[RequestHeaderKeys.Cookie] = cookies;

        return new RipInfo(images, dirName, FilenameScheme);
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
            if(!jsonData["v1080p"].IsNull())
            {
                videoUrl = jsonData["v1080p"]!.Deserialize<string>()!;
            }
            else if(!jsonData["v720p"].IsNull())
            {
                videoUrl = jsonData["v720p"]!.Deserialize<string>()!;
            }
            else if(!jsonData["v360p"].IsNull())
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
            Log.Information($"Parsing Chapter {counter}");
            counter += 1;
            soup = await Soupify(nextChapter.GetHref());
            var chapterImages = soup.SelectSingleNode("//div[@class='container-chapter-reader']")
                                  .SelectNodes(".//img");
            images.AddRange(chapterImages.Select(img => (StringImageLinkWrapper)img.GetSrc()));
            nextChapter = soup.SelectSingleNode("//a[@class='navi-change-chapter-btn-next a-h']");
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    /// <summary>
    ///     Parses the html for metarthunter.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> MetArtHunterParse()
    {
        return GenericHtmlParser("metarthunter");
    }
    
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
        var cookieValue = Config.Cookies["Newgrounds"];
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
                Log.Information("Parsing Art Post {i}/{numPosts}", i + 1, numPosts);
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
                Log.Information("Parsing Movie Post {i}/{numPosts}", i + 1, numPosts);
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

        // ReSharper disable once VariableHidesOuterVariable
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
            Log.Information("Parsing illustration posts page {count}", count);
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
        
        Log.Information("Parsing illustration posts...");
        var images = new List<StringImageLinkWrapper>();
        foreach (var (i, post) in posts.Enumerate())
        {
            Log.Information("Parsing illustration post {i}/{posts.Count}", i + 1, posts.Count);
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
            Log.Information("Parsing doujin post {i}/{posts.Count}", i + 1, posts.Count);
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

    /// <summary>
    ///     Parses the html for nlegs.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> NLegsParse()
    {
        const string domain = "https://www.nlegs.com";
        const int delay = 1000;
        
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//strong").InnerText;
        var numPages = soup.SelectSingleNode("//ul[@class='pagination pagination']")
                            .SelectNodes("./li")
                            .Count;
        var baseUrl = CurrentUrl.Split(".")[..^1].Join(".");
        var images = new List<StringImageLinkWrapper>();
        for (var i = 0; i < numPages; i++)
        {
            Log.Information("Parsing page {i} of {numPages}", i + 1, numPages);
            var posts = soup.SelectSingleNode("//div[@class='col-md-12 col-xs-12 ']")
                            .SelectNodes(".//a")
                            .Select(a => domain + a.GetHref())
                            .ToStringImageLinks();
            images.AddRange(posts);
            soup = await Soupify($"{baseUrl}/{i + 2}.html"); // Pages are 1-indexed
            await Task.Delay(delay);
        }

        var cookieJar = Driver.GetCookieJar();
        var cookies = cookieJar.AllCookies
                               .Aggregate("", (current, cookie) => current + $"{cookie.Name}={cookie.Value}; ")
                               .Trim();
        RequestHeaders[RequestHeaderKeys.Cookie] = cookies;
        
        return new RipInfo(images, dirName, FilenameScheme);
    }
    
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
            Log.Information("Parsing chapter {i} of {chapterCount}", i + 1, chapterCount);
            CurrentUrl = chapter;
            await LazyLoad(scrollBy: true, increment: 5000);
            soup = await Soupify();
            var post = soup.SelectSingleNode("//p[@class='flex flex-col justify-center items-center']");
            if (post is null)
            {
                Log.Warning("Post not found");
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
        var apiKey = Config.Keys["Pixeldrain"];
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
        var cookie = Config.Cookies["Porn3dx"];
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
            Log.Information("Parsing post {i} of {totalPosts}", i + 1, posts.Count);
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
    ///     Parses the html for pornavhd.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> PornAvHdParse()
    {
        var soup = await SolveParseAddCookies();
        var dirName = soup.SelectSingleNode("//h1[@itemprop='name']").InnerText;
        var iframe = soup.SelectSingleNode("//div[@class='responsive-player']/iframe");
        var iframeUrl = iframe.GetSrc();
        var (capturer, _) = await ConfigureNetworkCapture<SexBjCamVideoCapturer>();
        CurrentUrl = iframeUrl;
        var referer = iframeUrl.Split("/")[..3].Join("/") + '/';
        StringImageLinkWrapper playlist;
        while (true)
        {
            var links = capturer.GetNewVideoLinks();
            if (links.Count == 0)
            {
                continue;
            }

            playlist = new ImageLink(links[0], FilenameScheme, 0)
            {
                Referer = referer
            };
            break;
        }
        
        return new RipInfo([ playlist ], dirName, FilenameScheme);
    }
    
    /// <summary>
    ///     Parses the html for pornhub.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> PornhubParse()
    {
        var cookie = Config.Cookies["Pornhub"];
        var cookieJar = Driver.Manage().Cookies;
        cookieJar.AddCookie(new Cookie("il", cookie));
        cookieJar.AddCookie(new Cookie("accessAgeDisclaimerPH", "1"));
        cookieJar.AddCookie(new Cookie("adBlockAlertHidden", "1"));
        Driver.Refresh();
        var soup = await Soupify();
        string dirName;
        List<StringImageLinkWrapper> images;
        if (CurrentUrl.Contains("/model/") || CurrentUrl.Contains("/pornstar/"))
        {
            dirName = soup.SelectSingleNode("//h1[@itemprop='name']").InnerText;
            List<string> posts = [];
            var baseUrl = CurrentUrl.Split("/")[..5].Join("/");
            soup = await Soupify($"{baseUrl}/photos/public");
            var postNodes = soup.SelectNodes("//ul[@id='moreData']//a");
            if(postNodes is not null)
            {
                posts.AddRange(postNodes.Select(postNode => $"https://www.pornhub.com{postNode.GetHref()}"));
            }

            soup = await Soupify($"{baseUrl}/gifs/video");
            postNodes = soup.SelectNodes("//ul[@id='moreData']//a");
            if (postNodes is not null)
            {
                posts.AddRange(postNodes.Select(postNode => $"https://www.pornhub.com{postNode.GetHref()}"));
            }

            soup = await Soupify($"{baseUrl}/videos");
            postNodes = soup.SelectNodes("//ul[@id='uploadedVideosSection']//a");
            if (postNodes is not null)
            {
                posts.AddRange(postNodes.Select(postNode => $"https://www.pornhub.com{postNode.GetHref()}"));
            }
            
            while(true)
            {
                postNodes = soup.SelectNodes("//ul[@id='mostRecentVideosSection']//a");
                if (postNodes is null)
                {
                    break;
                }
                
                posts.AddRange(postNodes.Select(postNode => $"https://www.pornhub.com{postNode.GetHref()}"));
                var nextPage = soup.SelectSingleNode("//li[@class='page_next omega']");
                if (nextPage is null)
                {
                    break;
                }
                
                var nextPageUrl = nextPage.SelectSingleNode("./a").GetHref();
                soup = await Soupify($"https://www.pornhub.com{nextPageUrl}");
            }
            
            images = [];
            posts = posts.Where(post => !post.Contains("/channels/")
                                      && !post.Contains("/pornstar/")
                                      && !post.Contains("/model/")).ToList();
            foreach (var (i, post) in posts.Enumerate())
            {
                Log.Information("Parsing post {i}/{totalPosts}", i + 1, posts.Count);
                soup = await Soupify(post);
                var (postImages, _) = await PornhubLinkExtractor(soup);
                images.AddRange(postImages);
                if (i % 50 == 0)
                {
                    await Task.Delay(5000);
                }
            }
        }
        else
        {
            (images, dirName) = await PornhubLinkExtractor(soup);
        }
        return new RipInfo(images, dirName, FilenameScheme);
    }

    private async Task<(List<StringImageLinkWrapper> images, string dirName)> PornhubLinkExtractor(HtmlNode soup)
    {
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
        else if(CurrentUrl.Contains("/gif/"))
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
        else
        {
            throw new RipperException($"Unknown url: {CurrentUrl}");
        }

        return (images, dirName);
    }

    /// <summary>
    ///     Parses the html for porn-video-xxx.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> PornVideoXXXParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='blog-info--title']").InnerText;
        var images = soup.SelectNodes("//video/source")
                            .Select(source => source.GetSrc())
                            .ToStringImageLinkWrapperList();

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
    private Task<RipInfo> Rule34Parse()
    {
        return BooruParse("Rule34", "https://api.rule34.xxx/index.php?page=dapi&s=post&q=index&json=1&",
            "pid", 0, 1000);
    }

    /// <summary>
    ///     Parses the html for rule34video.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> Rule34VideoParse()
    {
        await Task.Delay(500);
        var continueButton = Driver.TryFindElement(By.XPath("//input[@name='continue']"));
        continueButton?.Click();
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='title']/span").InnerText;
        var videoPosts = new List<string>();
        while (true)
        {
            var videos = soup.SelectSingleNode("//div[@id='custom_list_videos_common_videos_items']")
                             .SelectNodes("./div")
                             .Select(div => div.SelectSingleNode("./a[@class='th js-open-popup']")?
                                                       .GetHref()
                                                       .DecodeUrl())
                             .Where(s => s is not null);
            videoPosts.AddRange(videos!);
            
            var uiBlock = Driver.TryFindElement(By.XPath("//div[@class='blockUI blockOverlay']"));
            while (uiBlock is not null)
            {
                await Task.Delay(500);
                uiBlock = Driver.TryFindElement(By.XPath("//div[@class='blockUI blockOverlay']"));
            }
            
            var nextButton = Driver.TryFindElement(By.XPath("//div[@class='item pager next']/a"));
            if (nextButton is null)
            {
                break;
            }
            nextButton.Click();
            soup = await Soupify(delay: 500);
        }
        
        var images = new List<StringImageLinkWrapper>();
        foreach (var post in videoPosts)
        {
            soup = await Soupify(post);
            var videoInfo = soup.SelectSingleNode("//div[@id='tab_video_info']");
            var downloads = videoInfo.SelectNodes("./div")[^1];
            var downloadLink = downloads.SelectSingleNode(".//a").GetHref(); // First link is the highest quality
            images.Add(downloadLink);
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    // TODO: See if this site has albums
    /// <summary>
    ///     Parses the html for site and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> Saint2Parse(string url)
    {
        if (url != "")
        {
            CurrentUrl = url;
        }
        
        var soup = await Soupify();
        string dirName;
        var images = new List<StringImageLinkWrapper>();
        if (CurrentUrl.Contains("/embed/"))
        {
            dirName = "Saint2 Video";
            var downloadLink = soup.SelectSingleNode("//a[@class='plyr__controls__item plyr__control']").GetHref();
            soup = await Soupify(downloadLink);
            var link = soup.SelectSingleNode("//a").GetHref();
            images.Add(link);
        }
        else
        {
            throw new RipperException($"Unhandled url: {CurrentUrl}");
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
    ///     Parses the html for sexbjcam.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> SexBjCamParser()
    {
        var soup = await SolveParseAddCookies(regenerateSessionOnFailure: true);
        //Driver.TakeDebugScreenshot();
        var dirName = soup.SelectSingleNode("//h1[@class='entry-title']").InnerText;
        var iframe = soup.SelectSingleNode("//iframe");
        var iframeUrl = iframe.GetSrc();
        var (capturer, _) = await ConfigureNetworkCapture<SexBjCamVideoCapturer>();
        CurrentUrl = iframeUrl;
        var referer = iframeUrl.Split("/")[..3].Join("/") + '/';
        StringImageLinkWrapper playlist;
        while (true)
        {
            var links = capturer.GetNewVideoLinks();
            if (links.Count == 0)
            {
                continue;
            }

            playlist = new ImageLink(links[0], FilenameScheme, 0)
            {
                Referer = referer
            };
            break;
        }
        
        return new RipInfo([ playlist ], dirName, FilenameScheme);
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

    /// <summary>
    ///     Parses the html for simpcity.su and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> SimpCityParse()
    {
        var cookieValue = Config.Cookies["SimpCity"];
        Driver.AddCookie("kZJdisc_user", cookieValue);
        var soup = await SolveParseAddCookies();
        var cookieJar = Driver.GetCookieJar();
        var cookies = cookieJar.AllCookies;
        var dirName = soup.SelectSingleNode("//h1[@class='p-title-value']").InnerText;
        var resolvableSet = new HashSet<string>
        {
            "bunkrrr.org",
            "bunkr.",
            /*"bunkr.ru",
            "bunkr.ws",
            "bunkr.red",
            "bunkr.media",
            "bunkr.si",*/
            "gofile.io",
            "pixeldrain.com",
            "cyberdrop.me",
            "jpg4.su"
        }.ToFrozenSet();
        var images = new List<StringImageLinkWrapper>();

        #region Parse Posts
        
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250,
            ScrollPauseTime = 1000
        };
        var resolveLists = new List<(List<string>, int)>();
        while (true)
        {
            if (Driver.Title == "SimpCity - Rate Limit")
            {
                Log.Warning("Rate limited, waiting 30 seconds");
                await Task.Delay(30000);
                Driver.Refresh();
                continue;
            }
            
            var posts = soup.SelectSingleNode("//div[@class='block-body js-replyNewMessageContainer']")
                            .SelectNodes("./article");
            foreach (var post in posts)
            {
                var content = post.SelectSingleNode(".//div[@class='bbWrapper']");
                var imgs = content.SelectNodesSafe(".//img")
                                  .Select(img => img.GetSrc().Remove(".md"))
                                  .Where(url => !url.StartsWith("data:image/gif")) // Ignore emojis
                                  .ToStringImageLinks();
                images.AddRange(imgs);
                var vids = content.SelectNodesSafe(".//video")
                                  .Select(VideoResolver)
                                  .ToStringImageLinks();
                images.AddRange(vids);
                var index = images.Count;
                var links = content.SelectNodesSafe(".//a");
                var resolve = links.Select(link => link.GetNullableHref())
                                   .OfType<string>()
                                   .Where(url => resolvableSet.Any(url.Contains))
                                   .ToList();
                var iframes = content.SelectNodesSafe(".//iframe[@class='saint-iframe']")
                                     .Select(iframe => iframe.GetSrc()); //cyberdrop and saint2
                resolve.AddRange(iframes);
                if (resolve.Count != 0)
                {
                    resolveLists.Add((resolve, index));
                }
            }
                
            // #if DEBUG
            // if (images.Any(l => l.StartsWith("data:")))
            // {
            //     Console.WriteLine("Data URL found");
            // }
            // #endif

            var nextPage = soup.SelectSingleNode("//a[@class='pageNav-jump pageNav-jump--next']");
            if (nextPage is null)
            {
                break;
            }
            
            var nextPageUrl = $"https://simpcity.su{nextPage.GetHref()}";
            Log.Information("Parsing page: {NextPageUrl}", nextPageUrl);
            soup = await Soupify(nextPageUrl, lazyLoadArgs: lazyLoadArgs);
        }

        #endregion
        
        #region Resolve Links

        var offset = 0;
        var resolved = 1;
        var total = resolveLists.Sum(list => list.Item1.Count);
        foreach (var (resolveList, i) in resolveLists)
        {
            var index = i + offset;
            foreach (var link in resolveList)
            {
                Log.Information("Resolving link {Resolved} of {Total}: {Link}", resolved, total, link);
                resolved++;
                Func<string, Task<RipInfo>> parser;
                if (link.Contains("bunkrrr.org") || link.Contains("bunkr.")/*link.Contains("bunkr.ru") || link.Contains("bunkr.ws")
                    || link.Contains("bunkr.red") || link.Contains("bunkr.media") || link.Contains("bunkr.si")*/)
                {
                    parser = BunkrParse;
                }
                else if (link.Contains("gofile.io"))
                {
                    parser = GoFileParse;
                }
                else if (link.Contains("pixeldrain.com"))
                {
                    parser = PixelDrainParse;
                }
                else if (link.Contains("cyberdrop.me"))
                {
                    parser = CyberDropParse;
                }
                else if (link.Contains("jpg4.su") || link.Contains("jpg5.su"))
                {
                    parser = Jpg5Parse;
                }
                else if(link.Contains("saint2."))
                {
                    parser = Saint2Parse;
                }
                else
                {
                    throw new RipperException($"Link bypassed filter: {link}");
                }

                RipInfo info;
                try
                {
                    info = await parser(link);
                }
                catch (WebDriverTimeoutException)
                {
                    Log.Warning("WebDriver unresponsive, retrying");
                    // Assume driver is dead and unreachable
                    // TODO: Find a better way to handle this
                    WebDriver.RegenerateDriver(false);
                    cookieJar = Driver.GetCookieJar();
                    foreach(var cookie in cookies)
                    {
                        cookieJar.AddCookie(cookie);
                    }
                    info = await parser(link);
                }
                catch (WebDriverException e) when (e.Message.Contains("The HTTP request to the remote WebDriver server for URL"))
                {
                    Log.Warning("WebDriver unresponsive, retrying");
                    // Assume driver is dead and unreachable
                    // TODO: Find a better way to handle this
                    //Driver = new FirefoxDriver(InitializeOptions(false));
                    WebDriver.RegenerateDriver(false);
                    cookieJar = Driver.GetCookieJar();
                    foreach(var cookie in cookies)
                    {
                        cookieJar.AddCookie(cookie);
                    }
                    info = await parser(link);
                }
                
                images.InsertRange(index, info.Urls.ToStringImageLinks());
                index += info.Urls.Count;
                offset += info.Urls.Count;
                await Task.Delay(125);
            }
        }

        #endregion

        return new RipInfo(images, dirName, FilenameScheme);

        string VideoResolver(HtmlNode video)
        {
            var src = video.GetNullableSrc();
            if (src is not null)
            {
                return src.Remove("-mobile");
            }

            var source = video.SelectSingleNode(".//source");
            src = source.GetSrc();
            if (!src.Contains("saint2.pk"))
            {
                return src;
            }

            var grandParent = video.ParentNode.ParentNode;
            var downloadLink = grandParent.SelectSingleNode(".//a[@class='plyr__controls__item plyr__control']").GetHref();
            var id = downloadLink.Split("/d/")[^1];
            return $"https://simp2.saint2.pk/api/download.php?file={id}";

        }
    }

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
    ///     Parses the html for 69tang.org and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> Six9TangParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='headline']/h1").InnerText;
        var video = soup.SelectSingleNode("//div[@class='fp-player']/video")
                         .GetSrc();

        return new RipInfo([ video ], dirName, FilenameScheme);
    }
    
    /// <summary>
    ///     Parses the html for spacemiss.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> SpaceMissParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='tdb-title-text']").InnerText;
        var images = soup
                    .SelectSingleNode(
                         "//figure[@class='wp-block-gallery has-nested-images columns-2 is-cropped td-modal-on-gallery wp-block-gallery-1 is-layout-flex wp-block-gallery-is-layout-flex']")
                    .SelectNodes(".//a")
                    .Select(img => img.GetHref())
                    .ToStringImageLinkWrapperList();

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
        var soup = await Soupify();
        var dirName = soup.SelectNodes("//h2[@class='section-title title']")[1].InnerText
                          .Split("Porn")[0]
                          .Split("porn")[0]
                          .Trim();
        var images = soup.SelectSingleNode("//div[@class='lightgallery thumbs quadruple fivefold']")
                         .SelectNodes(".//img")
                         .Select(img => img.GetSrc())
                         .ToStringImageLinkWrapperList();
        
        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for thothub.lol and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> ThothubParse()
    {
        const string sessionCookieName = "PHPSESSID";
        var cookieJar = Driver.GetCookieJar();
        var cookie = cookieJar.GetCookieNamed(sessionCookieName);
        if (cookie is not null)
        {
            cookieJar.DeleteCookie(cookie);
        }
        cookieJar.AddCookie(new Cookie(sessionCookieName, Config.Cookies["Thothub"]));
        Driver.Refresh();
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 625,
            ScrollPauseTime = 1
        };
        var soup = await Soupify(lazyLoadArgs: lazyLoadArgs, delay: 1000);
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
            while (true)
            {
                var posts = soup.SelectSingleNode("//div[@class='images']")
                                .SelectNodes(".//img")
                                .Select(img => img.GetSrc().Replace("/main/200x150/", "/sources/"))
                                .ToArray();
                if(posts.Any(p => p.Contains("data:")))
                {
                    await Task.Delay(1000);
                    ScrollToTop();
                    soup = await Soupify(lazyLoadArgs: lazyLoadArgs);
                    continue;
                }
                
                images = posts.ToStringImageLinkWrapperList();
                break;
            }
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for thotsbay.tv and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> ThotsBayParse()
    {
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250,
        };
        var soup = await Soupify(lazyLoadArgs: lazyLoadArgs);
        var dirName = soup.SelectSingleNode("//div[@class='actor-name']/h1").InnerText;
        var container = soup.SelectSingleNode("//div[@id='media-items-all']");
        var items = container.SelectNodes("./div")
                             .Select(div => $"https://thotsbay.tv{div.SelectSingleNode(".//a").GetHref()}");
        var images = new List<StringImageLinkWrapper>();
        foreach (var item in items)
        {
            soup = await Soupify(item, delay: 250);
        }
        
        // Unable to download videos from blob, parse in on hold

        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    /// <summary>
    ///     Parses the html for titsintops.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> TitsInTopsParse()
    {
        const string siteUrl = "https://titsintops.com";
        await SiteLogin();
        var cookies = Driver.GetCookieJar();
        var cookieStr = cookies.AllCookies.Aggregate("", (current, cookie) => current + $"{cookie.Name}={cookie.Value};");
        RequestHeaders["cookie"] = cookieStr;
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='p-title-value']")
                          .InnerText;
        var images = new List<StringImageLinkWrapper>();
        var externalLinks = CreateExternalLinkDict();
        var pageCount = 1;
        while (true)
        {
            Log.Information("Parsing page {PageCount}", pageCount);
            pageCount++;
            var posts = soup.SelectSingleNode("//div[@class='block-body js-replyNewMessageContainer']")
                            .SelectNodes(".//div[@class='message-content js-messageContent']");
            foreach (var post in posts)
            {
                var imgs = post.SelectSingleNode(".//article[@class='message-body js-selectToQuote']")
                               .SelectNodes(".//img");
                if (imgs is not null)
                {
                    var imgList = imgs.Select(im => im.GetSrc())
                                      .Where(im => im.Contains("http"));
                    images.AddRange(imgList.Select(im => (StringImageLinkWrapper)im));
                }
                
                var videos = post.SelectNodes(".//video");
                if (videos is not null)
                {
                    var videoUrls = videos.Select(vid => $"https://titsintops.com{vid.SelectSingleNode(".//source").GetSrc()}");
                    images.AddRange(videoUrls.Select(vid => (StringImageLinkWrapper)vid));
                }
                
                var iframes = post.SelectNodes(".//iframe");
                if (iframes is not null)
                {
                    var embeddedUrls = iframes.Select(em => em.GetSrc())
                                              .Where(em => em.Contains("http"));
                    embeddedUrls = await ParseEmbeddedUrls(embeddedUrls);
                    images.AddRange(embeddedUrls.Select(em => (StringImageLinkWrapper)em));
                }
                
                var attachments = post.SelectSingleNode(".//ul[@class='attachmentList']");
                if (attachments is not null)
                {
                    var attachments2 = attachments.SelectNodes(".//a[@class='file-preview js-lbImage']");
                    if (attachments2 is not null)
                    {
                        var attachList = attachments2.Select(attach => $"https://titsintops.com{attach.GetHref()}");
                        images.AddRange(attachList.Select(attach => (StringImageLinkWrapper)attach));
                    }
                }
                
                var links = post.SelectSingleNode(".//article[@class='message-body js-selectToQuote']")
                                .SelectNodes(".//a");
                if (links is not null)
                {
                    var linkList = links.Select(link => link.GetNullableHref())
                                        .Where(link => link is not null);
                    var filteredLinks = ExtractExternalUrls(linkList!);
                    var downloadableLinks = await ExtractDownloadableLinks(filteredLinks, externalLinks);
                    images.AddRange(downloadableLinks.Select(link => (StringImageLinkWrapper)link));
                }
            }

            var nextPage = soup.SelectSingleNode("//a[@class='pageNav-jump pageNav-jump--next']");
            if (nextPage is null)
            {
                SaveExternalLinks(externalLinks);
                break;
            }

            var nextPageUrl = nextPage.GetHref();
            soup = await Soupify($"{siteUrl}{nextPageUrl}");
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for toonily.me and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> ToonilyParse()
    {
        var btn = Driver.TryFindElement(By.XPath("//div[@id='show-more-chapters']"));
        if (btn is not null)
        {
            ScrollElementIntoView(btn);
            await Task.Delay(250);
            btn.Click();
            await Task.Delay(1000);
        }
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='name box']")
                          .SelectSingleNode(".//h1")
                          .InnerText;
        var chapterList = soup.SelectSingleNode("//ul[@id='chapter-list']")
                             .SelectNodes(".//li");
        var chapters = chapterList.Select(chapter => $"https://toonily.me{chapter.SelectSingleNode(".//a").GetHref()}");
        var images = new List<StringImageLinkWrapper>();
        foreach (var chapter in chapters.Reverse())
        {
            soup = await Soupify(chapter, lazyLoadArgs: new LazyLoadArgs {ScrollBy = true, Increment = 5000});
            var imageList = soup.SelectSingleNode("//div[@id='chapter-images']")
                                .SelectNodes(".//img");
            images.AddRange(imageList.Select(img => (StringImageLinkWrapper)img.GetSrc()));
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for tsumino.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> TsuminoParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='book-title']")
                          .InnerText;
        var numPages = int.Parse(soup.SelectSingleNode("//div[@id='Pages']")
                                  .InnerText
                                  .Trim());
        var pagerUrl = CurrentUrl.Replace("/entry/", "/Read/Index/") + "?page=";
        var images = new List<StringImageLinkWrapper>();
        for (var i = 1; i <= numPages; i++)
        {
            soup = await Soupify($"{pagerUrl}{i}", delay: 3000);
            var src = soup.SelectSingleNode("//img[@class='img-responsive reader-img']")
                          .GetSrc()
                          .Replace("&amp;", "&");
            images.Add(src);
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for twitter.com/x.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> TwitterParse()
    {
        # region Method-Global Variables

        var trueBaseWaitTime = 2500;
        var baseWaitTime = trueBaseWaitTime;
        const int maxWaitTime = 60000;
        var waitTime = baseWaitTime;
        const int maxRetries = 4;
        var streak = 0;

        # endregion

        var cookieValue = Config.Cookies["Twitter"];
        var cookieJar = Driver.GetCookieJar();
        cookieJar.AddCookie("auth_token", cookieValue);
        var dirName = CurrentUrl.Split("/")[3];
        if (!CurrentUrl.Contains("/media"))
        {
            CurrentUrl = $"https://twitter.com/{dirName}/media";
        }
        
        await Task.Delay(waitTime);
        var postLinks = new OrderedHashSet<string>();
        var newPosts = true;
        while (newPosts)
        {
            newPosts = false;
            var soup = await Soupify();
            var rows = soup.SelectSingleNode("//section")
                           .SelectSingleNode(".//div")
                           .SelectNodes("./div");
            foreach (var row in rows)
            {
                var posts = row.SelectNodes(".//li");
                foreach (var post in posts)
                {
                    var postLinkNode = post.SelectSingleNode(".//a");
                    if (postLinkNode is null)
                    {
                        continue;
                    }

                    var postLink = postLinkNode.GetHref();
                    if (postLinks.Add(postLink))
                    {
                        newPosts = true;
                    }
                }
            }

            if (newPosts)
            {
                ScrollPage();
                await Task.Delay(waitTime);
            }
        }

        var (capturer, bidi) = await ConfigureNetworkCapture<TwitterVideoCapturer>();
        // var capturer = new TwitterVideoCapturer();
        // var bidi = await Driver.AsBiDiAsync();
        // await bidi.Network.OnResponseCompletedAsync(capturer.CaptureHook);
        var images = new List<StringImageLinkWrapper>();
        var failedUrls = new List<(int, string)>();
        foreach (var (i, link) in postLinks.Enumerate())
        {
            var postLink = $"https://twitter.com{link}";
            Log.Information("Post {index}/{totalLinks}: {postLink}", i + 1, postLinks.Count, postLink);
            var (links, retryUrls) = await TwitterParserHelper(postLink, false);
            var videoLinks = capturer.GetNewVideoLinks();
            images.AddRange(videoLinks.ToStringImageLinks());
            
            var totalFound = images.Count;
            images.AddRange(links.ToStringImageLinks());
            
            foreach (var (j, url) in retryUrls)
            {
                failedUrls.Add((totalFound + j, url));
            }
        }
        
        if (failedUrls.Count > 0)
        {
            cookieJar.DeleteAllCookies();
            cookieJar.AddCookie("auth_token", cookieValue);
            capturer.Flush();
            await Task.Delay(600_000); // Wait 10 minutes before retrying failed links
            var failed = failedUrls.Select(url => url).ToList();
            failedUrls = [];
            var offset = 0;
            foreach(var (i, (index, link)) in failed.Enumerate())
            {
                Log.Information("Retrying failed post {index}/{totalLinks}: {postLink}", i + 1, failed.Count, link);
                var (links, retryUrls) = await TwitterParserHelper(link, true);
                var linkTemp = links.Select(x => (StringImageLinkWrapper)x);
                failedUrls.AddRange(retryUrls);
                images.InsertRange(index + offset, linkTemp);
                offset += links.Count;
            }
        }

        return new RipInfo(images, dirName, FilenameScheme);

        async Task<(List<string>, List<(int, string)>)> TwitterParserHelper(string postLink, bool logFailure)
        {
            var postImages = new List<string>();
            var failedUrls = new List<(int, string)>();
            var found = 0;
            for (var i = 0; i < maxRetries; i++)
            {
                var mediaFound = false;
                try
                {
                    var soup = await Soupify(postLink, delay: baseWaitTime, xpath: "//article");
                    var articles = soup.SelectNodes("//article");
                    foreach (var article in articles)
                    {
                        var content = article.SelectSingleNode("./div")
                                          .SelectSingleNode("./div");
                        var temp = content.SelectNodes("./div");
                        content = temp.Count > 2 ? temp[2] : temp[1];
                        temp = content.SelectNodes("./div");
                        if (temp.Count <= 1)
                        {
                            continue; // Most likely comment section
                        }
                        
                        content = content.SelectNodes("./div")[1];
                        var imgs = content.SelectNodesSafe(".//img");
                        foreach (var img in imgs)
                        {
                            var link = img.GetSrc().Replace("&amp;", "&");
                            if (link.Contains("/emoji/"))
                            {
                                continue;
                            }
                            
                            if(link.Contains("&name="))
                            {
                                link = link.Split("&name=")[0];
                                link = $"{link}&name=4096x4096"; // Get the highest resolution image
                            }
                            
                            postImages.Add(link);
                            found += 1;
                            mediaFound = true;
                        }
                    
                        var gifs = content.SelectNodesSafe(".//video");
                        foreach (var gif in gifs)
                        {
                            postImages.Add(gif.GetSrc());
                            found += 1;
                            mediaFound = true;
                        }
                    }
                    
                    break;
                }
                catch
                {
                    streak = 0;
                    var waitBoost = 0;
                    if (i == maxRetries - 1)
                    {
                        Log.Warning("Failed to get media: {PostLink}", postLink);
                        if (logFailure)
                        {
                            LogFailedUrl(postLink);
                        }
                        failedUrls.Add((found, postLink));
                        continue;
                    }

                    if (i == maxRetries - 2)
                    {
                        //baseWaitTime = waitTime
                        waitBoost = 300_000; // Wait 5 minutes before retrying
                        // Arbitrary, but should be enough time to get around rate limiting
                    }

                    waitTime = Math.Min(baseWaitTime * (int)Math.Pow(2, i), maxWaitTime);
                    var jitter = (int)(Random.Shared.NextDouble() * waitTime);
                    waitTime += jitter + waitBoost;
                    Log.Warning("Attempt {Attempt} failed. Retrying in {WaitTime:F2} seconds...", i + 1, waitTime / 1000.0);
                    await Task.Delay(waitTime);
                }

                if (!mediaFound)
                {
                    continue;
                }

                if (i == 0)
                {
                    streak += 1;
                    if (streak == 3)
                    {
                        //baseWaitTime *= 0.9; // Reduce wait time if successful on first try
                        //baseWaitTime = Math.Max(baseWaitTime, trueBaseWaitTime);
                    }
                }
                    
                break;
            }
            
            return (postImages, failedUrls);
        }
    }

    /// <summary>
    ///     Parses the html for wantedbabes.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> WantedBabesParse()
    {
        return GenericBabesHtmlParser("//div[@id='main-content']//h1", "//div[@class='gallery']");
    }

    private async Task<RipInfo> WnacgParse()
    {
        if (CurrentUrl.Contains("-slist-"))
        {
            CurrentUrl = CurrentUrl.Replace("-slist-", "-index-");
        }
        
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h2").InnerText;
        var numImages = soup.SelectSingleNode("//span[@class='name tb']").InnerText;
        var imageLinks = new List<string>();

        while (true)
        {
            var imageList = soup
                           .SelectNodes("//li[@class='li tb gallary_item']")
                           .Select(n => n.SelectSingleNode(".//a").GetHref());
            imageLinks.AddRange(imageList);
            var nextPageButton = soup.SelectSingleNode("//span[@class='next']");
            if (nextPageButton is null)
            {
                break;
            }
            
            var nextPageUrl = nextPageButton.SelectSingleNode(".//a").GetHref();
            soup = await Soupify($"https://www.wnacg.com{nextPageUrl}");
        }
        
        var images = new List<StringImageLinkWrapper>();
        foreach (var image in imageLinks)
        {
            soup = await Soupify($"https://www.wnacg.com{image}");
            var img = soup.SelectSingleNode("//img[@id='picarea']");
            var imgSrc = img.GetSrc();
            images.Add(imgSrc.Contains("https:") ? imgSrc : $"https:{imgSrc}");
        }
        
        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for v2ph.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> V2phParse()
    {
        // TODO: Work on bypassing cloudflare
        // The parser works, but when changing pages, the cf_clearance cookie gets refreshed in a way that causes issues
        var cookies = Config.Custom["V2PH"];
        var frontendCookie = cookies["frontend"];
        var frontendRmtCookie = cookies["frontend-rmt"];
        var cfClearanceCookie = cookies["cf_clearance"];
        var cookieJar = Driver.GetCookieJar();
        cookieJar.SetCookie("cf_clearance", cfClearanceCookie);
        Driver.Refresh();
        cookieJar.SetCookie("frontend", frontendCookie);
        cookieJar.SetCookie("frontend-rmt", frontendRmtCookie);
        Driver.Refresh();
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250,
            ScrollPauseTime = 750
        };
        var soup = await Soupify(lazyLoadArgs: lazyLoadArgs, delay: 1000);
        var dirName = soup.SelectSingleNode("//h1[@class='h5 text-center mb-3']")
                          .InnerText;
        var numPages = int.Parse(soup.SelectSingleNode("//dl[@class='row mb-0']")
                                  .SelectNodes(".//dd")[^1]
                                  .InnerText) / 10 + 1;
        var baseLink = CurrentUrl.Split("?")[0];
        var images = new List<StringImageLinkWrapper>();
        var parseComplete = false;
        for (var i = 0; i < numPages; i++)
        {
            if (i != 0)
            {
                var nextPage = $"{baseLink}?page={i + 1}";
                CurrentUrl = nextPage;
                var footer = Driver.TryFindElement(By.Id("footer-text"));
                if (footer is not null && footer.Text.Contains("Cloudflare"))
                {
                    cookieJar.SetCookie("cf_clearance", cfClearanceCookie);
                    Driver.Refresh();
                }
                soup = await Soupify(lazyLoadArgs: lazyLoadArgs, delay: 1000);
                Console.ReadLine();
            }

            List<StringImageLinkWrapper> imageList;
            while (true)
            {
                imageList = soup.SelectSingleNode("//div[@class='photos-list text-center']")
                                .SelectNodes(".//div[@class='album-photo my-2']")
                                .Select(img => img.SelectSingleNode(".//img").GetSrc())
                                .ToStringImageLinkWrapperList();
                if (imageList.Count == 0)
                {
                    parseComplete = true;
                    break;
                }
                
                if (imageList.All(img => !img.Contains("data:image/gif;base64")))
                {
                    break;
                }

                //Driver.FindElement(By.TagName("body")).SendKeys(Keys.Control + Keys.Home);
                ScrollToTop();
                soup = await Soupify(lazyLoadArgs: lazyLoadArgs);
            }
            
            images.AddRange(imageList);
            if (parseComplete)
            {
                break;
            }
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for xarthunter.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> XArtHunterParse()
    {
        return GenericHtmlParser("xarthunter");
    }

    /// <summary>
    ///     Parses the html for xasiat.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> XasiatParse()
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250,
        });
        var dirName = soup.SelectSingleNode("//div[@class='headline']/h1").InnerText;
        List<StringImageLinkWrapper> images;
        if (CurrentUrl.Contains("/albums/"))
        {
            images = soup.SelectSingleNode("//div[@class='images']")
                         .SelectNodes("./a")
                         .Select(a => a.GetHref())
                         .ToStringImageLinkWrapperList();
        }
        else if (CurrentUrl.Contains("/videos/"))
        {
            var playButton = Driver.FindElement(By.XPath("//a[@class='fp-play']"));
            Driver.ScrollElementIntoView(playButton);
            Driver.Click(playButton);
            var exists = await WaitForElement("//video");
            if (!exists)
            {
                throw new RipperException("Video not found");
            }

            var player = Driver.FindElement(By.Id("kt_player"));
            var qualityButton = Driver.TryFindElement(By.XPath("//a[@class='fp-settings']"));
            if (qualityButton is not null)
            {
                var classes = player.GetAttribute("class")!;
                classes += " is-settings-open";
                Driver.ExecuteScript($"document.getElementById('kt_player').setAttribute('class', '{classes}')");
                var bestQuality = Driver.TryFindElement(By.XPath("//div[@class='fp-settings-list-item is-hd']/a"));
                if (bestQuality is not null)
                {
                    Driver.Click(bestQuality);
                }
                else
                {
                    Log.Warning("No HD quality found, using default");
                }
            }
            
            await Task.Delay(1000);
            var video = Driver.TryFindElement(By.XPath("//video"));
            var src = video!.GetSrc()!;
            images = [src];
        }
        else
        {
            throw new RipperException("Unknown URL type");
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for en.xchina.co and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> XChinaParse()
    {
        string GetVideoUrl(HtmlNode soup)
        {
            var video = soup.SelectSingleNode("//video");
            var videoSrc = video.GetSrc();
            if (!videoSrc.StartsWith("blob:"))
            {
                return videoSrc;
            }

            var script = video.ParentNode.SelectNodes(".//script")[1].InnerText;
            var url = script.Split("hls.loadSource(\"")[1];
            url = url.Split("\");")[0];
            return url;

        }
        
        var soup = await Soupify();
        var tabContents = soup.SelectSingleNode("//div[@class='tab-content video-info']");
        var publisherNode = tabContents.SelectSingleNode(".//i[@class='fa fa-video-camera']") 
                            ?? tabContents.SelectSingleNode(".//i[@class='fa fa-user-circle']");

        var publisher = publisherNode.ParentNode.SelectSingleNode(".//a").InnerText;
        
        var series = tabContents.SelectSingleNode(".//i[@class='fa fa-file-o']");
        var id = series is not null ? series.ParentNode.InnerText : CurrentUrl.Split("id-")[^1].Split(".")[0];
        var title = tabContents.SelectNodes(".//div")[0].InnerText;
        var dirName = $"[{publisher}] {id} - {title}";
        var images = new List<StringImageLinkWrapper>();
        if (CurrentUrl.Contains("/video/"))
        {
            var url = GetVideoUrl(soup);
            images.Add(url);
        }
        else
        {
            int numVids;
            var controls = soup.SelectSingleNode("//div[@class='controls']");
            if (controls is not null)
            {
                var index = controls.SelectSingleNode(".//div[@class='index']")
                                    .InnerText
                                    .Split(" ")[^1];
                numVids = int.Parse(index);
            }
            else
            {
                var vid = soup.SelectSingleNode("//div[@class='container']//video");
                numVids = vid is not null ? 1 : 0;
            }

            var prevUrl = "";
            for (var i = 0; i < numVids; i++)
            {
                var vidSrc = GetVideoUrl(soup);
                while (vidSrc == prevUrl)
                {
                    await Task.Delay(250);
                    // var vidNode = Driver.FindElement(By.XPath("//video"));
                    // vidSrc = vidNode.GetSrc()!;
                    soup = await Soupify();
                    vidSrc = GetVideoUrl(soup);
                }
                
                images.Add(vidSrc);
                prevUrl = vidSrc;
                var nextButton = Driver.TryFindElement(By.XPath("//div[@go='1']"));
                nextButton?.Click();
            }

            while (true)
            {
                var photos = soup.SelectSingleNode("//div[@class='photos']")
                                 .SelectNodes("./a")
                                 .SelectMany(a => a.SelectNodes(".//img"))
                                 .Select(img => img.GetSrc().Split("_")[0] + ".jpg");
                images.AddRange(photos.Select(photo => (StringImageLinkWrapper)photo));

                var nextButton = soup.SelectSingleNode("//a[@class='next']");
                if (nextButton is null)
                {
                    break;
                }
                
                var nextPage = nextButton.GetNullableHref();
                if (nextPage is not null)
                {
                    soup = await Soupify("https://en.xchina.co" + nextPage);
                }
                else
                {
                    break;
                }
            }
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for xiuren.biz and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> XiurenParse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='jeg_post_title']").InnerText;
        var images = soup.SelectSingleNode("//div[@class='content-inner ']")
                         .SelectNodes(".//a")
                         .Select(img => img.GetHref())
                         .ToStringImageLinkWrapperList();

        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    /// <summary>
    ///     Parses the html for xmissy.nl and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> XMissyParse()
    {
        var loadButton = Driver.TryFindElement(By.Id("loadallbutton"));
        loadButton?.Click();
        await Task.Delay(1000);
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@id='pagetitle']")
                          .InnerText;
        var images = soup.SelectSingleNode("//div[@id='gallery']")
                            .SelectNodes(".//div[@class='noclick-image']")
                            .Select(img => img.SelectSingleNode(".//img").GetNullableSrc() ?? img.SelectSingleNode(".//img").GetSrc())
                            .ToStringImageLinkWrapperList();

        return new RipInfo(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Parses the html for yande.re and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private Task<RipInfo> YandeParse()
    {
        return BooruParse("Yande.re", "https://yande.re/post.json?", "page", 
            1, 100);
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

    private async Task<HtmlNode> Soupify(int delay = 0, LazyLoadArgs? lazyLoadArgs = null, string xpath = "")
    {
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
    
    private async Task<HtmlNode> Soupify(string url, int delay = 0, LazyLoadArgs? lazyLoadArgs = null, 
                                                string xpath = "", bool urlString = true)
    {
        if (!urlString)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(url);
            return doc.DocumentNode;
        }
        
        CurrentUrl = url;

        return await Soupify(delay: delay, lazyLoadArgs: lazyLoadArgs, xpath: xpath);
    }

    private static async Task<HtmlNode> Soupify(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(content);
        return htmlDocument.DocumentNode;
    }
    
    private static async Task<HtmlNode> Soupify(Solution solution)
    {
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(solution.Response);
        return htmlDocument.DocumentNode;
    }
    
    /// <summary>
    ///     Wait for an element to exist on the page
    /// </summary>
    /// <param name="xpath">XPath of the element to wait for</param>
    /// <param name="delay">Delay between each check</param>
    /// <param name="timeout">Timeout for the wait (-1 for no timeout)</param>
    /// <returns>True if the element exists, false if the timeout is reached</returns>
    private async Task<bool> WaitForElement(string xpath, float delay = 0.1f, float timeout = 10)
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
    
    private void CleanTabs(string urlMatch)
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

    /// <summary>
    ///     Get captcha solution to site, parse the html, and add the necessary cookies to the driver.
    /// </summary>
    /// <param name="regenerateSessionOnFailure">Whether to regenerate the session if getting the solution fails</param>
    /// <returns>The parsed html</returns>
    private async Task<HtmlNode> SolveParseAddCookies(bool regenerateSessionOnFailure = false)
    {
        Solution solution;
        try
        {
            solution = await FlareSolverrManager.GetSiteSolution(CurrentUrl);
        }
        catch (FailedToGetSolutionException)
        {
            if (regenerateSessionOnFailure)
            {
                await FlareSolverrManager.DeleteSession(suppressException: true);
                solution = await FlareSolverrManager.GetSiteSolution(CurrentUrl);
            }
            else
            {
                throw;
            }
        }
        
        var cookieJar = Driver.GetCookieJar();
        foreach (var seleniumCookie in solution.Cookies.Select(cookie => cookie.ToSeleniumCookie()))
        {
            cookieJar.AddCookie(seleniumCookie);
        }
        
        return await Soupify(solution);
    }

    private async Task<HtmlNode> SolveCaptcha(string url, bool humanSolving)
    {
        await SolveParseAddCookies(); // Replace with a proper captcha solver
        if (humanSolving)
        {
            Log.Information("Please solve the captcha and press enter to continue...");
            Console.ReadLine();
        }
        
        return await Soupify(url);
    }
    
    private async Task<(T, BiDi)> ConfigureNetworkCapture<T>() where T : PlaylistCapturer, new()
    {
        var capturer = new T();
        var bidi = await Driver.AsBiDiAsync();
        await bidi.Network.OnResponseCompletedAsync(capturer.CaptureHook);
        return (capturer, bidi);
    }
    
    private static async Task<List<string>> ParseEmbeddedUrls(IEnumerable<string> urls)
    {
        var parsedUrls = new List<string>();
        var imgurKey = Config.Keys["Imgur"];
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = $"Client-Id {imgurKey}"
        };
        var client = new HttpClient();
        foreach (var url in urls)
        {
            if (!url.Contains("imgur"))
            {
                continue;
            }

            var response = await client.GetAsync(url);
            var soup = await Soupify(response: response);
            var imgurUrl = soup.SelectSingleNode("//a[@id='image-link']")
                               .GetHref();
            var imageHash = imgurUrl.Split("#")[^1];
            var message = headers.ToRequest(HttpMethod.Get, $"https://api.imgur.com/3/image/{imageHash}");
            response = await client.SendAsync(message);
            var responseJson = await response.Content.ReadFromJsonAsync<JsonNode>();
            parsedUrls.Add(responseJson!["data"]!["link"]!.Deserialize<string>()!);
        }
        
        return parsedUrls;
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

    private static Dictionary<string, List<string>> ExtractExternalUrls(IEnumerable<string> urls)
    {
        var externalLinks = CreateExternalLinkDict();
        var urlList = urls.ToList();
        foreach (var site in externalLinks.Keys)
        {
            foreach (var link in urlList.Where(url => !string.IsNullOrEmpty(url) && url.Contains(site))
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

                    var link = UrlUtility.ExtractUrl(part);
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
    private Task LazyLoad(LazyLoadArgs args)
    {
        return args.StopElement is not null && Driver.TryFindElement(args.StopElement) is not null
            ? LazyLoad(args.StopElement) 
            : LazyLoad(args.ScrollBy, args.Increment, args.ScrollPauseTime, args.ScrollBack, args.ReScroll);
    }
    
    /// <summary>
    ///     Scroll through the page to lazy load images
    /// </summary>
    /// <param name="scrollBy">Whether to scroll through the page or instantly scroll to the bottom</param>
    /// <param name="increment">Distance to scroll by each iteration</param>
    /// <param name="scrollPauseTime">Seconds to wait between each scroll</param>
    /// <param name="scrollBack">Distance to scroll back by after reaching the bottom of the page</param>
    /// <param name="rescroll">Whether scrolling through the page again</param>
    private async Task LazyLoad(bool scrollBy = false, int increment = 2500, int scrollPauseTime = 500,
                                       int scrollBack = 0, bool rescroll = false)
    {
        var lastHeight = Driver.GetScrollHeight();
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
    }

    private async Task LazyLoad(By elementToFind, int increment = 1250, int scrollPauseTime = 500)
    {
        var scrollScript = $"window.scrollBy({{top: {increment}, left: 0, behavior: 'smooth'}});";
        while (true)
        {
            Driver.ExecuteScript(scrollScript);
            await Task.Delay(scrollPauseTime);
            var element = Driver.FindElement(elementToFind);
            if (element.Displayed)
            {
                break;
            }
        }
    }
    
    private void ScrollPage(int distance = 1250)
    {
        var currHeight = (long)Driver.ExecuteScript("return window.pageYOffset");
        var scrollScript = $"window.scrollBy({{top: {currHeight + distance}, left: 0, behavior: 'smooth'}});";
        Driver.ExecuteScript(scrollScript);
    }
    
    private void ScrollToTop()
    {
        Driver.ExecuteScript("window.scrollTo(0, 0);");
    }
    
    private void ScrollElementIntoView(IWebElement element)
    {
        Driver.ExecuteScript("arguments[0].scrollIntoView(true);", element);
    }
    
    private static void LogFailedUrl(string url)
    {
        File.AppendAllText("failed.txt", $"{url}\n");
    }

    #region Parser Testing

    public async Task<RipInfo> TestParse(string givenUrl, bool debug, bool printSite)
    {
        try
        {
            /*var options = InitializeOptions(debug);
            Driver = new FirefoxDriver(options);*/
            CurrentUrl = givenUrl.Replace("members.", "www.");
            SiteName = TestSiteCheck(givenUrl);

            Log.Debug("Testing: {SiteName}Parse", SiteName);
            Log.Debug("URL: {CurrentUrl}", CurrentUrl);
            var start = DateTime.Now;
            var data = await EvaluateParser(SiteName);
            var end = DateTime.Now;
            Log.Debug("Referer: {Referer}", data.Urls[0].Referer);
            Log.Debug("Time Elapsed: {TimeElapsed}", end - start);
            var outData = data.Urls.Select(d => d.Url).ToList();
            JsonUtility.Serialize("test.json", outData);
            if (debug)
            {
                NicheImageRipper.LogMessageToFile("Press any key to exit...", LogEventLevel.Debug);
                Console.ReadKey();
            }

            return data;
        }
        catch(Exception e)
        {
            Log.Error(e, "Error occurred while testing {SiteName}Parse", SiteName);
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

            //await FlareSolverrManager.DeleteSession();
            Driver.Quit();
        }
    }

    private Task<RipInfo> EvaluateParser(string siteName)
    {
        siteName = TestSiteConverter(siteName);
        siteName = siteName[0].ToString().ToUpper() + siteName[1..];
        var methodName = $"{siteName}Parse";
        try
        {
            var method = GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method != null)
            {
                // The second parameter is null because the method has no parameters
                return (Task<RipInfo>)(method.Invoke(this, null) ?? new InvalidOperationException());
            }
        }
        catch (AmbiguousMatchException)
        {
            // Should be handled by the following code
        }

        var methods = typeof(HtmlParser).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Where(m => m.GetParameters().Length == 0);
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
        Log.Error("Method {MethodName} not found.", methodName);
        throw new InvalidOperationException();
    }

    private static string TestSiteConverter(string siteName)
    {
        if (siteName == "x")
        {
            return "twitter";
        }
        
        if (siteName.Contains("bunkrrr"))
        {
            siteName = siteName.Replace("bunkrrr", "Bunkr");
        }
        else if (siteName.Contains("100bucksbabes"))
        {
            siteName = siteName.Replace("100bucksbabes", "HundredBucksBabes");
        }
        else if (siteName.Contains("chapmanganato"))
        {
            siteName = siteName.Replace("chapmanganato", "Manganato");
        }
        else if(siteName.Contains("18kami"))
        {
            siteName = siteName.Replace("18kami", "EighteenKami");
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

    #endregion
    
    public void Dispose()
    {
        if (Config.CloseFlareSolverrSession)
        {
            FlareSolverrManager.DeleteSession().Wait();
        }
        
        GC.SuppressFinalize(this);
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
    [GeneratedRegex(@"vvv=([^&]+).+t=([^&]+)")]
    private static partial Regex Av19APlaylistIdRegex();
}