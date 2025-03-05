using System.Net.Http.Json;
using System.Reflection;
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
using Core.SiteParsing.HtmlParsers;
using Core.Utility;
using FlareSolverrIntegration.Responses;
using HtmlAgilityPack;
using OpenQA.Selenium;
using OpenQA.Selenium.BiDi;
using OpenQA.Selenium.Firefox;
using Serilog;
using Serilog.Events;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing;

public abstract partial class HtmlParser : IDisposable
{
    protected const string Protocol = "https:";

    protected static readonly string[] ExternalSites =
        ["drive.google.com", "mega.nz", "mediafire.com", "sendvid.com", "dropbox.com"];

    protected static readonly string[] ParsableSites = ["drive.google.com", "mega.nz", "sendvid.com", "dropbox.com"];
    
    protected static Config Config => Config.Instance;
    
    //public static Dictionary<string, bool> SiteLoginStatus { get; set; } = new();
    
    protected WebDriver WebDriver { get; set; }
    public bool Interrupted { get; set; }
    protected string SiteName { get; set; }
    public float SleepTime { get; set; }
    public float Jitter { get; set; }
    protected string GivenUrl { get; set; }
    protected FilenameScheme FilenameScheme { get; set; }
    protected Dictionary<string, string> RequestHeaders { get; set; }

    protected FirefoxDriver Driver => WebDriver.Driver;

    protected string CurrentUrl
    {
        get => Driver.Url;
        set => Driver.Url = value;
    }
    protected static bool Debugging { get; set; }
    protected static FlareSolverrManager FlareSolverrManager => NicheImageRipper.FlareSolverrManager;
    protected static string UserAgent => Config.UserAgent;

    protected HtmlParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "",
                         FilenameScheme filenameScheme = FilenameScheme.Original)
    {
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
        WebDriver.RegenerateDriver(debug);
        Debugging = debug;
    }
    
    public async Task<RipInfo> ParseSite(string url)
    {
        Log.Debug("Parsing {Url}", url);
        if (File.Exists("partial.json"))
        {
            Log.Debug("Partial save file found");
            var saveData = ReadPartialSave();
            if (saveData.TryGetValue(url, out var value))
            {
                Log.Debug("Partial save found for {Url}", url);
                RequestHeaders["cookie"] = value.Cookies;
                RequestHeaders["referer"] = value.Referer;
                Interrupted = true;
                return value.RipInfo;
            }
        }

        Log.Debug("No partial save found for site; Parsing site");
        url = url.Replace("members.", "www.");
        GivenUrl = url;
        (SiteName, SleepTime) = UrlUtility.SiteCheck(GivenUrl, RequestHeaders);
        if (SiteName != "booru")
        {
            CurrentUrl = url;
        }
        
        // Log.Debug("Getting parser for {SiteName}", SiteName);
        // var siteParser = GetParser(SiteName);
        try
        {
            Log.Debug("Executing parser for {SiteName}", SiteName);
            var siteInfo = await Parse();
            Log.Debug("Saving partial save for {Url}", url);
            WritePartialSave(siteInfo, url);
            //pickle.dump(self.driver.get_cookies(), open("cookies.pkl", "wb"))
            return siteInfo;
        }
        catch(Exception e)
        {
            Log.Error(e, "Failed to parse {CurrentUrl}", CurrentUrl);
            #if DEBUG
            Driver.TakeDebugScreenshot();
            await File.WriteAllTextAsync("test.html", Driver.PageSource);
            #endif
            throw;
        }
    }

    public static HtmlParser GetParser(string siteName, WebDriver webDriver, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original)
    {
        return siteName switch
        {
            "imhentai" => new ImhentaiParser(webDriver, requestHeaders, "imhentai", filenameScheme),
            "kemono" => new KemonoParser(webDriver, requestHeaders, "kemono", filenameScheme),
            "coomer" => new CoomerParser(webDriver, requestHeaders, "coomer", filenameScheme),
            "sankakucomplex" => new SankakuComplexParser(webDriver, requestHeaders, "sankakucomplex", filenameScheme),
            "omegascans" => new OmegaScansParser(webDriver, requestHeaders, "omegascans", filenameScheme),
            "redgifs" => new RedGifsParser(webDriver, requestHeaders, "redgifs", filenameScheme),
            "rule34" => new Rule34Parser(webDriver, requestHeaders, "rule34", filenameScheme),
            "gelbooru" => new GelbooruParser(webDriver, requestHeaders, "gelbooru", filenameScheme),
            "danbooru" => new DanbooruParser(webDriver, requestHeaders, "danbooru", filenameScheme),
            "google" => new GoogleParser(webDriver, requestHeaders, "google", filenameScheme),
            "dropbox" => new DropboxParser(webDriver, requestHeaders, "dropbox", filenameScheme),
            "imgur" => new ImgurParser(webDriver, requestHeaders, "imgur", filenameScheme),
            "newgrounds" => new NewgroundsParser(webDriver, requestHeaders, "newgrounds", filenameScheme),
            "wnacg" => new WnacgParser(webDriver, requestHeaders, "wnacg", filenameScheme),
            "arca" => new ArcaParser(webDriver, requestHeaders, "arca", filenameScheme),
            "babecentrum" => new BabeCentrumParser(webDriver, requestHeaders, "babecentrum", filenameScheme),
            "babeimpact" => new BabeImpactParser(webDriver, requestHeaders, "babeimpact", filenameScheme),
            "babeuniversum" => new BabeUniversumParser(webDriver, requestHeaders, "babeuniversum", filenameScheme),
            "babesandbitches" => new BabesAndBitchesParser(webDriver, requestHeaders, "babesandbitches", filenameScheme),
            "babesandgirls" => new BabesAndGirlsParser(webDriver, requestHeaders, "babesandgirls", filenameScheme),
            "babesaround" => new BabesAroundParser(webDriver, requestHeaders, "babesaround", filenameScheme),
            "babesbang" => new BabesBangParser(webDriver, requestHeaders, "babesbang", filenameScheme),
            "babesinporn" => new BabesInPornParser(webDriver, requestHeaders, "babesinporn", filenameScheme),
            "babesmachine" => new BabesMachineParser(webDriver, requestHeaders, "babesmachine", filenameScheme),
            "bestprettygirl" => new BestPrettyGirlParser(webDriver, requestHeaders, "bestprettygirl", filenameScheme),
            "bitchesgirls" => new BitchesGirlsParser(webDriver, requestHeaders, "bitchesgirls", filenameScheme),
            "bunkr" => new BunkrParser(webDriver, requestHeaders, "bunkr", filenameScheme),
            "buondua" => new BuonduaParser(webDriver, requestHeaders, "buondua", filenameScheme),
            "bustybloom" => new BustyBloomParser(webDriver, requestHeaders, "bustybloom", filenameScheme),
            "camwhores" => new CamwhoresParser(webDriver, requestHeaders, "camwhores", filenameScheme),
            "cherrynudes" => new CherryNudesParser(webDriver, requestHeaders, "cherrynudes", filenameScheme),
            "chickteases" => new ChickTeasesParser(webDriver, requestHeaders, "chickteases", filenameScheme),
            "cool18" => new Cool18Parser(webDriver, requestHeaders, "cool18", filenameScheme),
            "cutegirlporn" => new CuteGirlPornParser(webDriver, requestHeaders, "cutegirlporn", filenameScheme),
            "cyberdrop" => new CyberDropParser(webDriver, requestHeaders, "cyberdrop", filenameScheme),
            "decorativemodels" => new DecorativeModelsParser(webDriver, requestHeaders, "decorativemodels", filenameScheme),
            //DeviantArt
            "dirtyyoungbitches" => new DirtyYoungBitchesParser(webDriver, requestHeaders, "dirtyyoungbitches", filenameScheme),
            "eahentai" => new EahentaiParser(webDriver, requestHeaders, "eahentai", filenameScheme),
            "8boobs" => new EightBoobsParser(webDriver, requestHeaders, "8boobs", filenameScheme),
            "8muses" => new EightMusesParser(webDriver, requestHeaders, "8muses", filenameScheme),
            "elitebabes" => new EliteBabesParser(webDriver, requestHeaders, "elitebabes", filenameScheme),
            "erosberry" => new ErosBerryParser(webDriver, requestHeaders, "erosberry", filenameScheme),
            "erohive" => new EroHiveParser(webDriver, requestHeaders, "erohive", filenameScheme),
            "erome" => new EroMeParser(webDriver, requestHeaders, "erome", filenameScheme),
            "erothots" => new EroThotsParser(webDriver, requestHeaders, "erothots", filenameScheme),
            "everia" => new EveriaParser(webDriver, requestHeaders, "everia", filenameScheme),
            "exgirlfriendmarket" => new ExGirlFriendMarketParser(webDriver, requestHeaders, "exgirlfriendmarket", filenameScheme),
            "fapello" => new FapelloParser(webDriver, requestHeaders, "fapello", filenameScheme),
            "faponic" => new FaponicParser(webDriver, requestHeaders, "faponic", filenameScheme),
            "f5girls" => new F5GirlsParser(webDriver, requestHeaders, "f5girls", filenameScheme),
            "femjoyhunter" => new FemJoyHunterParser(webDriver, requestHeaders, "femjoyhunter", filenameScheme),
            "flickr" => new FlickrParser(webDriver, requestHeaders, "flickr", filenameScheme),
            "foxhq" => new FoxHqParser(webDriver, requestHeaders, "foxhq", filenameScheme),
            "ftvhunter" => new FtvHunterParser(webDriver, requestHeaders, "ftvhunter", filenameScheme),
            "ggoorr" => new GgoorrParser(webDriver, requestHeaders, "ggoorr", filenameScheme),
            "girlsofdesire" => new GirlsOfDesireParser(webDriver, requestHeaders, "girlsofdesire", filenameScheme),
            "girlsreleased" => new GirlsReleasedParser(webDriver, requestHeaders, "girlsreleased", filenameScheme),
            "glam0ur" => new Glam0urParser(webDriver, requestHeaders, "glam0ur", filenameScheme),
            "grabpussy" => new GrabPussyParser(webDriver, requestHeaders, "grabpussy", filenameScheme),
            "gyrls" => new GyrlsParser(webDriver, requestHeaders, "gyrls", filenameScheme),
            "hegrehunter" => new HegreHunterParser(webDriver, requestHeaders, "hegrehunter", filenameScheme),
            "hentai-cosplays" => new HentaiCosplaysParser(webDriver, requestHeaders, "hentai-cosplays", filenameScheme),
            "hentairox" => new HentaiRoxParser(webDriver, requestHeaders, "hentairox", filenameScheme),
            "hustlebootytemptats" => new HustleBootyTempTatsParser(webDriver, requestHeaders, "hustlebootytemptats", filenameScheme),
            "hotgirl" => new HotGirlParser(webDriver, requestHeaders, "hotgirl", filenameScheme),
            "hotstunners" => new HotStunnersParser(webDriver, requestHeaders, "hotstunners", filenameScheme),
            "hottystop" => new HottyStopParser(webDriver, requestHeaders, "hottystop", filenameScheme),
            "100bucksbabes" => new HundredBucksBabesParser(webDriver, requestHeaders, "100bucksbabes", filenameScheme),
            "imgbox" => new ImgBoxParser(webDriver, requestHeaders, "imgbox", filenameScheme),
            "influencersgonewild" => new InfluencersGoneWildParser(webDriver, requestHeaders, "influencersgonewild", filenameScheme),
            "inven" => new InvenParser(webDriver, requestHeaders, "inven", filenameScheme),
            "jkforum" => new JkForumParser(webDriver, requestHeaders, "jkforum", filenameScheme),
            "join2babes" => new Join2BabesParser(webDriver, requestHeaders, "join2babes", filenameScheme),
            "joymiihub" => new JoyMiiHubParser(webDriver, requestHeaders, "joymiihub", filenameScheme),
            "leakedbb" => new LeakedBbParser(webDriver, requestHeaders, "leakedbb", filenameScheme),
            "livejasminbabes" => new LiveJasminBabesParser(webDriver, requestHeaders, "livejasminbabes", filenameScheme),
            "luscious" => new LusciousParser(webDriver, requestHeaders, "luscious", filenameScheme),
            "mainbabes" => new MainBabesParser(webDriver, requestHeaders, "mainbabes", filenameScheme),
            "manganato" or "chapmanganato" => new ManganatoParser(webDriver, requestHeaders, "manganato", filenameScheme),
            "metarthunter" => new MetArtHunterParser(webDriver, requestHeaders, "metarthunter", filenameScheme),
            "morazzia" => new MorazziaParser(webDriver, requestHeaders, "morazzia", filenameScheme),
            "myhentaigallery" => new MyHentaiGalleryParser(webDriver, requestHeaders, "myhentaigallery", filenameScheme),
            "micmicdoll" => new MicMicDollParser(webDriver, requestHeaders, "micmicdoll", filenameScheme),
            "nakedgirls" => new NakedGirlsParser(webDriver, requestHeaders, "nakedgirls", filenameScheme),
            "nhentai" => new NHentaiParser(webDriver, requestHeaders, "nhentai", filenameScheme),
            "nightdreambabe" => new NightDreamBabeParser(webDriver, requestHeaders, "nightdreambabe", filenameScheme),
            "nijie" => new NijieParser(webDriver, requestHeaders, "nijie", filenameScheme),
            "animeh" => new AnimehParser(webDriver, requestHeaders, "animeh", filenameScheme),
            "novoglam" => new NovoGlamParser(webDriver, requestHeaders, "novoglam", filenameScheme),
            "novohot" => new NovoHotParser(webDriver, requestHeaders, "novohot", filenameScheme),
            "novoporn" => new NovoPornParser(webDriver, requestHeaders, "novoporn", filenameScheme),
            "nudebird" => new NudeBirdParser(webDriver, requestHeaders, "nudebird", filenameScheme),
            "nudity911" => new Nudity911Parser(webDriver, requestHeaders, "nudity911", filenameScheme),
            "pbabes" => new PBabesParser(webDriver, requestHeaders, "pbabes", filenameScheme),
            "pixeldrain" => new PixelDrainParser(webDriver, requestHeaders, "pixeldrain", filenameScheme),
            "pmatehunter" => new PMateHunterParser(webDriver, requestHeaders, "pmatehunter", filenameScheme),
            "porn3dx" => new Porn3dxParser(webDriver, requestHeaders, "porn3dx", filenameScheme),
            "pornhub" => new PornhubParser(webDriver, requestHeaders, "pornhub", filenameScheme),
            "putmega" => new PutMegaParser(webDriver, requestHeaders, "putmega", filenameScheme),
            "rabbitsfun" => new RabbitsFunParser(webDriver, requestHeaders, "rabbitsfun", filenameScheme),
            "redpornblog" => new RedPornBlogParser(webDriver, requestHeaders, "redpornblog", filenameScheme),
            "rossoporn" => new RossoPornParser(webDriver, requestHeaders, "rossoporn", filenameScheme),
            "sensualgirls" => new SensualGirlsParser(webDriver, requestHeaders, "sensualgirls", filenameScheme),
            "sexhd" => new SexHdParser(webDriver, requestHeaders, "sexhd", filenameScheme),
            "sexyaporno" => new SexyAPornoParser(webDriver, requestHeaders, "sexyaporno", filenameScheme),
            "sexybabesart" => new SexyBabesArtParser(webDriver, requestHeaders, "sexybabesart", filenameScheme),
            "sexykittenporn" => new SexyKittenPornParser(webDriver, requestHeaders, "sexykittenporn", filenameScheme),
            "sexynakeds" => new SexyNakedsParser(webDriver, requestHeaders, "sexynakeds", filenameScheme),
            "sfmcompile" => new SfmCompileParser(webDriver, requestHeaders, "sfmcompile", filenameScheme),
            "silkengirl" => new SilkenGirlParser(webDriver, requestHeaders, "silkengirl", filenameScheme),
            "simply-cosplay" => new SimplyCosplayParser(webDriver, requestHeaders, siteName, filenameScheme),
            "sxchinesegirlz01" => new SxChineseGirlz01Parser(webDriver, requestHeaders, "sxchinesegirlz01", filenameScheme),
            "pleasuregirl" => new PleasureGirlParser(webDriver, requestHeaders, "pleasuregirl", filenameScheme),
            "theomegaproject" => new TheOmegaProjectParser(webDriver, requestHeaders, "theomegaproject", filenameScheme),
            "thothub" => new ThothubParser(webDriver, requestHeaders, "thothub", filenameScheme),
            "titsintops" => new TitsInTopsParser(webDriver, requestHeaders, "titsintops", filenameScheme),
            "toonily" => new ToonilyParser(webDriver, requestHeaders, "toonily", filenameScheme),
            "tsumino" => new TsuminoParser(webDriver, requestHeaders, "tsumino", filenameScheme),
            "twitter" => new TwitterParser(webDriver, requestHeaders, "twitter", filenameScheme),
            "x" => new TwitterParser(webDriver, requestHeaders, "x", filenameScheme),
            "wantedbabes" => new WantedBabesParser(webDriver, requestHeaders, "wantedbabes", filenameScheme),
            "xarthunter" => new XArtHunterParser(webDriver, requestHeaders, "xarthunter", filenameScheme),
            "xmissy" => new XMissyParser(webDriver, requestHeaders, "xmissy", filenameScheme),
            "yande" => new YandeParser(webDriver, requestHeaders, "yande", filenameScheme),
            "18kami" => new EighteenKamiParser(webDriver, requestHeaders, "18kami", filenameScheme),
            "cup2d" => new Cup2DParser(webDriver, requestHeaders, "cup2d", filenameScheme),
            "5ge" => new FiveGeParser(webDriver, requestHeaders, "5ge", filenameScheme),
            "japaneseasmr" => new JapaneseAsmrParser(webDriver, requestHeaders, "japaneseasmr", filenameScheme),
            "spacemiss" => new SpaceMissParser(webDriver, requestHeaders, "spacemiss", filenameScheme),
            "xiuren" => new XiurenParser(webDriver, requestHeaders, "xiuren", filenameScheme),
            "xchina" => new XChinaParser(webDriver, requestHeaders, "xchina", filenameScheme),
            "gofile" => new GoFileParser(webDriver, requestHeaders, "gofile", filenameScheme),
            "jpg5" => new Jpg5Parser(webDriver, requestHeaders, "jpg5", filenameScheme),
            "simpcity" => new SimpCityParser(webDriver, requestHeaders, "simpcity", filenameScheme),
            "rule34video" => new Rule34VideoParser(webDriver, requestHeaders, "rule34video", filenameScheme),
            "av19a" => new Av19aParser(webDriver, requestHeaders, "av19a", filenameScheme),
            "eporner" => new EpornerParser(webDriver, requestHeaders, "eporner", filenameScheme),
            "cgcosplay" => new CgCosplayParser(webDriver, requestHeaders, "cgcosplay", filenameScheme),
            "4khd" => new FourKHdParser(webDriver, requestHeaders, "4khd", filenameScheme),
            "cosplay69" => new Cosplay69Parser(webDriver, requestHeaders, "cosplay69", filenameScheme),
            "nlegs" => new NLegsParser(webDriver, requestHeaders, "nlegs", filenameScheme),
            "ladylap" => new LadyLapParser(webDriver, requestHeaders, "ladylap", filenameScheme),
            "xasiat" => new XasiatParser(webDriver, requestHeaders, "xasiat", filenameScheme),
            "catbox" => new CatBoxParser(webDriver, requestHeaders, "catbox", filenameScheme),
            "jrants" => new JRantsParser(webDriver, requestHeaders, "jrants", filenameScheme),
            "sexbjcam" => new SexBjCamParser(webDriver, requestHeaders, "sexbjcam", filenameScheme),
            "pornavhd" => new PornAvHdParser(webDriver, requestHeaders, "pornavhd", filenameScheme),
            "knit" => new KnitParser(webDriver, requestHeaders, "knit", filenameScheme),
            "69tang" => new Six9TangParser(webDriver, requestHeaders, "69tang", filenameScheme),
            "jieav" => new JieAvParser(webDriver, requestHeaders, "jieav", filenameScheme),
            "hentaiclub" => new HentaiClubParser(webDriver, requestHeaders, "hentaiclub", filenameScheme),
            "avav19" => new Avav19Parser(webDriver, requestHeaders, "avav19", filenameScheme),
            "booru" => new AllBooruParser(webDriver, requestHeaders, "booru", filenameScheme),
            "mangadex" => new MangaDexParser(webDriver, requestHeaders, "mangadex", filenameScheme),
            "cosblay" => new CosblayParser(webDriver, requestHeaders, "cosblay", filenameScheme),
            "kaizty" => new KaiztyParser(webDriver, requestHeaders, "kaizty", filenameScheme),
            "quatvn" => new QuatvnParser(webDriver, requestHeaders, "quatvn", filenameScheme),
            _ => throw new RipperException($"Site not supported/implemented: {siteName}")
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

    protected Task<bool> SiteLogin()
    {
        Log.Debug("Checking if already logged in to {SiteName}", SiteName);
        if (IsLoggedInToSite(SiteName))
        {
            Log.Debug("Already logged in to {SiteName}", SiteName);
            return Task.FromResult(true);
        }

        Log.Debug("Logging in to {SiteName}", SiteName);
        var loginTask = SiteLoginHelper();
        
        return loginTask.ContinueWith(task =>
        {
            WebDriver.SiteLoginStatus[SiteName] = task.Result;
            Log.Debug("Logged in to {SiteName}: {Result}", SiteName, task.Result);
            return task.Result;
        });
    }
    
    protected virtual Task<bool> SiteLoginHelper()
    {
        throw new Exception("Site authentication not implemented");
    }

    private bool IsLoggedInToSite(string siteName)
    {
        var siteLoginStatus = WebDriver.SiteLoginStatus;
        return !siteLoginStatus.TryAdd(siteName, false) && siteLoginStatus[siteName];
    }

    public abstract Task<RipInfo> Parse();

    #region Generic Site Parsers

    /// <summary>
    ///     Parses the html for kemono.su and coomer.su and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <param name="domainUrl">The domain url of the site</param>
    /// <returns></returns>
    protected async Task<RipInfo> DotPartyParse(string domainUrl)
    {
        var baseUrl = CurrentUrl;
        var urlSplit = baseUrl.Split("/");
        var sourceSite = urlSplit[3];
        baseUrl = string.Join("/", urlSplit[3..6]).Split("?")[0];
        baseUrl = $"{domainUrl}/api/v1/{baseUrl}";
        await WaitForElement("//h1[@id='user-header__info-top']");
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@id='user-header__info-top']")
                          .SelectSingleNode(".//span[@itemprop='name']").InnerText;
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
                var dropboxParser = new DropboxParser(WebDriver, RequestHeaders, "dropbox", FilenameScheme);
                var ripInfo = await dropboxParser.Parse(link);
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
    protected async Task<RipInfo> GenericBabesHtmlParser(string dirNameXpath, string imageContainerXpath)
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

    protected Task<RipInfo> GenericHtmlParser(string siteName)
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
    protected async Task<RipInfo> BooruParse(Booru site, string? tags = null)
    {
        var metadata = site.GetMetadata();
        var siteName = metadata.SiteName;
        var baseUrl = metadata.BaseUrl;
        var pageParameterName = metadata.PageParameterName;
        var startingPageIndex = metadata.StartingPageIndex;
        var limit = metadata.Limit;
        var headers = metadata.Headers;
        var jsonObjectNavigation = metadata.JsonObjectNavigation;
        tags ??= BooruRegex().Match(CurrentUrl).Groups[1].Value;
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
            try
            {
                json = await response.Content.ReadFromJsonAsync<JsonNode>();
            }
            catch (JsonException e) when(e.Message.StartsWith("The input does not contain any JSON tokens."))
            {
                Log.Debug("Failed to deserialize json due to empty response");
                return RipInfo.Empty;
            }
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

    protected static string ExtractJsonObject(string json)
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

    protected async Task<HtmlNode> Soupify(int delay = 0, LazyLoadArgs? lazyLoadArgs = null, string xpath = "", int xpathTimout = 10)
    {
        if (delay > 0)
        {
            await Task.Delay(delay);
        }

        if (xpath != "")
        {
            await WaitForElement(xpath, timeout: xpathTimout);
        }

        if (lazyLoadArgs is not null)
        {
            await LazyLoad(lazyLoadArgs);
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(Driver.PageSource);
        return doc.DocumentNode;
    }
    
    protected async Task<HtmlNode> Soupify(string url, int delay = 0, LazyLoadArgs? lazyLoadArgs = null, 
                                           string xpath = "", bool urlString = true, ICookieJar? cookies = null)
    {
        if (!urlString)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(url);
            return doc.DocumentNode;
        }
        
        CurrentUrl = url;
        
        if (cookies is not null)
        {
            var cookieJar = Driver.GetCookieJar();
            foreach (var cookie in cookies.AllCookies)
            {
                cookieJar.AddCookie(cookie);
            }
        }

        return await Soupify(delay: delay, lazyLoadArgs: lazyLoadArgs, xpath: xpath);
    }

    private static async Task<HtmlNode> Soupify(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(content);
        return htmlDocument.DocumentNode;
    }

    private static Task<HtmlNode> Soupify(Solution solution)
    {
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(solution.Response);
        return Task.FromResult(htmlDocument.DocumentNode);
    }
    
    /// <summary>
    ///     Wait for an element to exist on the page
    /// </summary>
    /// <param name="xpath">XPath of the element to wait for</param>
    /// <param name="delay">Delay between each check</param>
    /// <param name="timeout">Timeout for the wait (-1 for no timeout)</param>
    /// <returns>True if the element exists, false if the timeout is reached</returns>
    protected async Task<bool> WaitForElement(string xpath, float delay = 0.1f, float timeout = 10)
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
    
    protected void CleanTabs(string urlMatch)
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
    ///     Solves a CAPTCHA using FlareSolverr, parses the returned HTML, and adds the necessary cookies to the browser session.
    /// </summary>
    /// <param name="regenerateSessionOnFailure">
    ///     If <c>true</c>, regenerates the session and retries once if CAPTCHA solving fails.
    /// </param>
    /// <param name="cookies">
    ///     Optional. A list of cookie dictionaries to include in the session when solving the CAPTCHA.
    /// </param>
    /// <returns>
    ///     The parsed HTML document as an <see cref="HtmlNode"/>.
    /// </returns>
    /// <exception cref="FeatureNotAvailableException">
    ///     Thrown if FlareSolverr support is not available.
    /// </exception>
    /// <exception cref="FailedToGetSolutionException">
    ///     Thrown if CAPTCHA solving fails and session regeneration is disabled.
    /// </exception>
    protected async Task<HtmlNode> SolveParseAddCookies(bool regenerateSessionOnFailure = false, 
                                                        List<Dictionary<string, string>>? cookies = null)
    {
        if (!NicheImageRipper.AvailableFeatures.HasFlag(ExternalFeatureSupport.FlareSolverr))
        {
            throw new FeatureNotAvailableException(ExternalFeatureSupport.FlareSolverr);
        }
        
        Solution solution;
        try
        {
            solution = await FlareSolverrManager.GetSiteSolution(CurrentUrl, cookies);
        }
        catch (FailedToGetSolutionException)
        {
            if (regenerateSessionOnFailure)
            {
                await FlareSolverrManager.DeleteSession(suppressException: true);
                solution = await FlareSolverrManager.GetSiteSolution(CurrentUrl, cookies);
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

    protected async Task<(T, BiDi)> ConfigureNetworkCapture<T>() where T : PlaylistCapturer, new()
    {
        var capturer = new T();
        var bidi = await Driver.AsBiDiAsync();
        await bidi.Network.OnResponseCompletedAsync(capturer.CaptureHook);
        return (capturer, bidi);
    }

    protected static async Task<List<string>> ParseEmbeddedUrls(IEnumerable<string> urls)
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

    protected static Dictionary<string, List<string>> CreateExternalLinkDict()
    {
        var externalLinks = new Dictionary<string, List<string>>();
        foreach (var site in ExternalSites)
        {
            externalLinks[site] = [];
        }

        return externalLinks;
    }

    protected static Dictionary<string, List<string>> ExtractExternalUrls(IEnumerable<string> urls)
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

    protected static void SaveExternalLinks(Dictionary<string, List<string>> links)
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

    protected static async Task<List<string>> ExtractDownloadableLinks(Dictionary<string, List<string>> srcDict,
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
    protected Task LazyLoad(LazyLoadArgs args)
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
    protected async Task LazyLoad(bool scrollBy = false, int increment = 2500, int scrollPauseTime = 500,
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

    protected async Task LazyLoad(By elementToFind, int increment = 1250, int scrollPauseTime = 500)
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

    protected void ScrollPage(int distance = 1250)
    {
        var currHeight = (long)Driver.ExecuteScript("return window.pageYOffset");
        var scrollScript = $"window.scrollBy({{top: {currHeight + distance}, left: 0, behavior: 'smooth'}});";
        Driver.ExecuteScript(scrollScript);
    }

    protected void ScrollToTop()
    {
        Driver.ExecuteScript("window.scrollTo(0, 0);");
    }

    protected void ScrollElementIntoView(IWebElement element)
    {
        Driver.ExecuteScript("arguments[0].scrollIntoView(true);", element);
    }

    protected static void LogFailedUrl(string url)
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
        var className = $"{siteName}Parser";
        var classType = Assembly.GetExecutingAssembly()
                             .GetTypes()
                             .FirstOrDefault(t => string.Equals(t.Name, className, StringComparison.OrdinalIgnoreCase));
        if (classType is not null)
        {
            var ripper = (HtmlParser)Activator.CreateInstance(classType, WebDriver, RequestHeaders, siteName, FilenameScheme)!;
            return ripper.Parse();
        }

        // Handle the case where the method does not exist
        Log.Error("Parser {ParserName} not found.", className);
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
    protected static partial Regex BooruRegex();
}