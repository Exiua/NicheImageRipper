using System.Reflection;
using System.Text.RegularExpressions;
using Common.ExtensionMethods;
using Core.Configuration;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.Managers;
using Core.SiteParsing.HtmlParsers;
using Core.SiteParsing.VideoCapturers;
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

    protected static readonly string[] EXTERNAL_SITES =
        ["drive.google.com", "mega.nz", "mediafire.com", "sendvid.com", "dropbox.com"];
    
    protected static GeneralConfig Config => Configuration.Config.Instance;

    //public static Dictionary<string, bool> SiteLoginStatus { get; set; } = new();

    protected WebDriver WebDriver { get; }
    public bool Interrupted { get; set; }
    private string SiteName { get; set; }
    public float SleepTime { get; set; }
    public float Jitter { get; set; }
    protected string GivenUrl { get; private set; }
    protected FilenameScheme FilenameScheme { get; }
    protected Dictionary<string, string> RequestHeaders { get; }

    protected FirefoxDriver Driver => WebDriver.Driver;

    protected string CurrentUrl
    {
        get => Driver.Url;
        set => Driver.Url = value;
    }

    protected static bool Debugging { get; set; }
    protected static FlareSolverrManager FlareSolverrManager => NicheImageRipper.FlareSolverrManager;
    protected static string UserAgent => Config.UserAgent;

    protected HtmlParser(WebDriver driver, Dictionary<string, string> requestHeaders,
                         FilenameScheme filenameScheme = FilenameScheme.Original)
    {
        WebDriver = driver;
        RequestHeaders = requestHeaders;
        FilenameScheme = filenameScheme;
        Interrupted = false;
        SiteName = "";
        SleepTime = 0.2f;
        Jitter = 0.5f;
        GivenUrl = "";
    }

    public async Task<RipInfo> ParseSite(string url)
    {
        Log.Debug("Parsing {Url}", url);
        url = url.Replace("members.", "www.") // For HAnime
                 .Replace("exhentai.org", "e-hentai.org"); // Need to go through e-hentai first for cookies
        GivenUrl = url;
        (SiteName, SleepTime) = UrlUtility.SiteCheck(GivenUrl, RequestHeaders);
        // e-hentai image links expire too quickly, so we need to parse the site every time
        if (File.Exists("partial.json") && (SiteName != "e-hentai" && SiteName != "exhentai"))
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
        catch (Exception e)
        {
            Log.Error(e, "Failed to parse {CurrentUrl}", CurrentUrl);
            #if DEBUG
            await File.WriteAllTextAsync("test.html", Driver.PageSource);
            Driver.TakeDebugScreenshot();
            #endif
            throw;
        }
    }

    public static HtmlParser GetParser(string siteName, WebDriver webDriver, Dictionary<string, string> requestHeaders,
                                       FilenameScheme filenameScheme = FilenameScheme.Original)
    {
        return siteName switch
        {
            "imhentai" => new ImhentaiParser(webDriver, requestHeaders, filenameScheme),
            "kemono" => new KemonoParser(webDriver, requestHeaders, filenameScheme),
            "coomer" => new CoomerParser(webDriver, requestHeaders, filenameScheme),
            "sankakucomplex" => new SankakuComplexParser(webDriver, requestHeaders, filenameScheme),
            "omegascans" => new OmegaScansParser(webDriver, requestHeaders, filenameScheme),
            "redgifs" => new RedGifsParser(webDriver, requestHeaders, filenameScheme),
            "rule34" => new Rule34Parser(webDriver, requestHeaders, filenameScheme),
            "gelbooru" => new GelbooruParser(webDriver, requestHeaders, filenameScheme),
            "danbooru" => new DanbooruParser(webDriver, requestHeaders, filenameScheme),
            "google" => new GoogleParser(webDriver, requestHeaders, filenameScheme),
            "dropbox" => new DropboxParser(webDriver, requestHeaders, filenameScheme),
            "imgur" => new ImgurParser(webDriver, requestHeaders, filenameScheme),
            "newgrounds" => new NewgroundsParser(webDriver, requestHeaders, filenameScheme),
            "wnacg" => new WnacgParser(webDriver, requestHeaders, filenameScheme),
            "arca" => new ArcaParser(webDriver, requestHeaders, filenameScheme),
            "babecentrum" => new BabeCentrumParser(webDriver, requestHeaders, filenameScheme),
            "babeimpact" => new BabeImpactParser(webDriver, requestHeaders, filenameScheme),
            "babeuniversum" => new BabeUniversumParser(webDriver, requestHeaders, filenameScheme),
            "babesandbitches" => new BabesAndBitchesParser(webDriver, requestHeaders, filenameScheme),
            "babesandgirls" => new BabesAndGirlsParser(webDriver, requestHeaders, filenameScheme),
            "babesaround" => new BabesAroundParser(webDriver, requestHeaders, filenameScheme),
            "babesbang" => new BabesBangParser(webDriver, requestHeaders, filenameScheme),
            "babesinporn" => new BabesInPornParser(webDriver, requestHeaders, filenameScheme),
            "babesmachine" => new BabesMachineParser(webDriver, requestHeaders, filenameScheme),
            "bestprettygirl" => new BestPrettyGirlParser(webDriver, requestHeaders, filenameScheme),
            "bitchesgirls" => new BitchesGirlsParser(webDriver, requestHeaders, filenameScheme),
            "bunkr" => new BunkrParser(webDriver, requestHeaders, filenameScheme),
            "buondua" => new BuonduaParser(webDriver, requestHeaders, filenameScheme),
            "bustybloom" => new BustyBloomParser(webDriver, requestHeaders, filenameScheme),
            "camwhores" => new CamwhoresParser(webDriver, requestHeaders, filenameScheme),
            "cherrynudes" => new CherryNudesParser(webDriver, requestHeaders, filenameScheme),
            "chickteases" => new ChickTeasesParser(webDriver, requestHeaders, filenameScheme),
            "cool18" => new Cool18Parser(webDriver, requestHeaders, filenameScheme),
            "cutegirlporn" => new CuteGirlPornParser(webDriver, requestHeaders, filenameScheme),
            "cyberdrop" => new CyberDropParser(webDriver, requestHeaders, filenameScheme),
            "decorativemodels" => new DecorativeModelsParser(webDriver, requestHeaders, filenameScheme),
            //DeviantArt
            "dirtyyoungbitches" => new DirtyYoungBitchesParser(webDriver, requestHeaders, filenameScheme),
            "e-hentai" or "exhentai" => new EHentaiParser(webDriver, requestHeaders, filenameScheme),
            "eahentai" => new EahentaiParser(webDriver, requestHeaders, filenameScheme),
            "8boobs" => new EightBoobsParser(webDriver, requestHeaders, filenameScheme),
            "8muses" => new EightMusesParser(webDriver, requestHeaders, filenameScheme),
            "elitebabes" => new EliteBabesParser(webDriver, requestHeaders, filenameScheme),
            "erosberry" => new ErosBerryParser(webDriver, requestHeaders, filenameScheme),
            "erohive" => new EroHiveParser(webDriver, requestHeaders, filenameScheme),
            "erome" => new EroMeParser(webDriver, requestHeaders, filenameScheme),
            "erothots" => new EroThotsParser(webDriver, requestHeaders, filenameScheme),
            "everia" => new EveriaParser(webDriver, requestHeaders, filenameScheme),
            "exgirlfriendmarket" => new ExGirlFriendMarketParser(webDriver, requestHeaders, filenameScheme),
            "fapello" => new FapelloParser(webDriver, requestHeaders, filenameScheme),
            "faponic" => new FaponicParser(webDriver, requestHeaders, filenameScheme),
            "f5girls" => new F5GirlsParser(webDriver, requestHeaders, filenameScheme),
            "femjoyhunter" => new FemJoyHunterParser(webDriver, requestHeaders, filenameScheme),
            "flickr" => new FlickrParser(webDriver, requestHeaders, filenameScheme),
            "foxhq" => new FoxHqParser(webDriver, requestHeaders, filenameScheme),
            "ftvhunter" => new FtvHunterParser(webDriver, requestHeaders, filenameScheme),
            "ggoorr" => new GgoorrParser(webDriver, requestHeaders, filenameScheme),
            "girlsofdesire" => new GirlsOfDesireParser(webDriver, requestHeaders, filenameScheme),
            "girlsreleased" => new GirlsReleasedParser(webDriver, requestHeaders, filenameScheme),
            "glam0ur" => new Glam0urParser(webDriver, requestHeaders, filenameScheme),
            "grabpussy" => new GrabPussyParser(webDriver, requestHeaders, filenameScheme),
            "gyrls" => new GyrlsParser(webDriver, requestHeaders, filenameScheme),
            "hegrehunter" => new HegreHunterParser(webDriver, requestHeaders, filenameScheme),
            "hentai-cosplays" => new HentaiCosplaysParser(webDriver, requestHeaders, filenameScheme),
            "hentairox" => new HentaiRoxParser(webDriver, requestHeaders, filenameScheme),
            "hustlebootytemptats" => new HustleBootyTempTatsParser(webDriver, requestHeaders, filenameScheme),
            "hotgirl" => new HotGirlParser(webDriver, requestHeaders, filenameScheme),
            "hotstunners" => new HotStunnersParser(webDriver, requestHeaders, filenameScheme),
            "hottystop" => new HottyStopParser(webDriver, requestHeaders, filenameScheme),
            "100bucksbabes" => new HundredBucksBabesParser(webDriver, requestHeaders, filenameScheme),
            "imgbox" => new ImgBoxParser(webDriver, requestHeaders, filenameScheme),
            "influencersgonewild" => new InfluencersGoneWildParser(webDriver, requestHeaders, filenameScheme),
            "inven" => new InvenParser(webDriver, requestHeaders, filenameScheme),
            "jkforum" => new JkForumParser(webDriver, requestHeaders, filenameScheme),
            "join2babes" => new Join2BabesParser(webDriver, requestHeaders, filenameScheme),
            "joymiihub" => new JoyMiiHubParser(webDriver, requestHeaders, filenameScheme),
            "leakedbb" => new LeakedBbParser(webDriver, requestHeaders, filenameScheme),
            "livejasminbabes" => new LiveJasminBabesParser(webDriver, requestHeaders, filenameScheme),
            "luscious" => new LusciousParser(webDriver, requestHeaders, filenameScheme),
            "mainbabes" => new MainBabesParser(webDriver, requestHeaders, filenameScheme),
            "manganato" or "chapmanganato" => new ManganatoParser(webDriver, requestHeaders, filenameScheme),
            "metarthunter" => new MetArtHunterParser(webDriver, requestHeaders, filenameScheme),
            "morazzia" => new MorazziaParser(webDriver, requestHeaders, filenameScheme),
            "myhentaigallery" => new MyHentaiGalleryParser(webDriver, requestHeaders, filenameScheme),
            "micmicdoll" => new MicMicDollParser(webDriver, requestHeaders, filenameScheme),
            "nakedgirls" => new NakedGirlsParser(webDriver, requestHeaders, filenameScheme),
            "nhentai" => new NHentaiParser(webDriver, requestHeaders, filenameScheme),
            "nightdreambabe" => new NightDreamBabeParser(webDriver, requestHeaders, filenameScheme),
            "nijie" => new NijieParser(webDriver, requestHeaders, filenameScheme),
            "novoglam" => new NovoGlamParser(webDriver, requestHeaders, filenameScheme),
            "novohot" => new NovoHotParser(webDriver, requestHeaders, filenameScheme),
            "novoporn" => new NovoPornParser(webDriver, requestHeaders, filenameScheme),
            "nudebird" => new NudeBirdParser(webDriver, requestHeaders, filenameScheme),
            "nudity911" => new Nudity911Parser(webDriver, requestHeaders, filenameScheme),
            "pbabes" => new PBabesParser(webDriver, requestHeaders, filenameScheme),
            "pixeldrain" => new PixelDrainParser(webDriver, requestHeaders, filenameScheme),
            "pmatehunter" => new PMateHunterParser(webDriver, requestHeaders, filenameScheme),
            "porn3dx" => new Porn3dxParser(webDriver, requestHeaders, filenameScheme),
            "pornhub" => new PornhubParser(webDriver, requestHeaders, filenameScheme),
            "putmega" => new PutMegaParser(webDriver, requestHeaders, filenameScheme),
            "rabbitsfun" => new RabbitsFunParser(webDriver, requestHeaders, filenameScheme),
            "redpornblog" => new RedPornBlogParser(webDriver, requestHeaders, filenameScheme),
            "rossoporn" => new RossoPornParser(webDriver, requestHeaders, filenameScheme),
            "sensualgirls" => new SensualGirlsParser(webDriver, requestHeaders, filenameScheme),
            "sexhd" => new SexHdParser(webDriver, requestHeaders, filenameScheme),
            "sexyaporno" => new SexyAPornoParser(webDriver, requestHeaders, filenameScheme),
            "sexybabesart" => new SexyBabesArtParser(webDriver, requestHeaders, filenameScheme),
            "sexykittenporn" => new SexyKittenPornParser(webDriver, requestHeaders, filenameScheme),
            "sexynakeds" => new SexyNakedsParser(webDriver, requestHeaders, filenameScheme),
            "sfmcompile" => new SfmCompileParser(webDriver, requestHeaders, filenameScheme),
            "silkengirl" => new SilkenGirlParser(webDriver, requestHeaders, filenameScheme),
            "simply-cosplay" => new SimplyCosplayParser(webDriver, requestHeaders, filenameScheme),
            "sxchinesegirlz01" => new SxChineseGirlz01Parser(webDriver, requestHeaders, filenameScheme),
            "pleasuregirl" => new PleasureGirlParser(webDriver, requestHeaders, filenameScheme),
            "theomegaproject" => new TheOmegaProjectParser(webDriver, requestHeaders, filenameScheme),
            "thothub" => new ThothubParser(webDriver, requestHeaders, filenameScheme),
            "titsintops" => new TitsInTopsParser(webDriver, requestHeaders, filenameScheme),
            "toonily" => new ToonilyParser(webDriver, requestHeaders, filenameScheme),
            "tsumino" => new TsuminoParser(webDriver, requestHeaders, filenameScheme),
            "twitter" or "x" => new TwitterParser(webDriver, requestHeaders, filenameScheme),
            "xcancel" => new XCancelParser(webDriver, requestHeaders, filenameScheme),
            "wantedbabes" => new WantedBabesParser(webDriver, requestHeaders, filenameScheme),
            "xarthunter" => new XArtHunterParser(webDriver, requestHeaders, filenameScheme),
            "xmissy" => new XMissyParser(webDriver, requestHeaders, filenameScheme),
            "yande" => new YandeParser(webDriver, requestHeaders, filenameScheme),
            "18kami" => new EighteenKamiParser(webDriver, requestHeaders, filenameScheme),
            "cup2d" => new Cup2DParser(webDriver, requestHeaders, filenameScheme),
            "5ge" => new FiveGeParser(webDriver, requestHeaders, filenameScheme),
            "japaneseasmr" => new JapaneseAsmrParser(webDriver, requestHeaders, filenameScheme),
            "spacemiss" => new SpaceMissParser(webDriver, requestHeaders, filenameScheme),
            "xiuren" => new XiurenParser(webDriver, requestHeaders, filenameScheme),
            "xchina" => new XChinaParser(webDriver, requestHeaders, filenameScheme),
            "gofile" => new GoFileParser(webDriver, requestHeaders, filenameScheme),
            "jpg5" => new Jpg5Parser(webDriver, requestHeaders, filenameScheme),
            "simpcity" => new SimpCityParser(webDriver, requestHeaders, filenameScheme),
            "rule34video" => new Rule34VideoParser(webDriver, requestHeaders, filenameScheme),
            "av19a" => new Av19aParser(webDriver, requestHeaders, filenameScheme),
            "eporner" => new EpornerParser(webDriver, requestHeaders, filenameScheme),
            "cgcosplay" => new CgCosplayParser(webDriver, requestHeaders, filenameScheme),
            "4khd" => new FourKHdParser(webDriver, requestHeaders, filenameScheme),
            "cosplay69" => new Cosplay69Parser(webDriver, requestHeaders, filenameScheme),
            "nlegs" => new NLegsParser(webDriver, requestHeaders, filenameScheme),
            "ladylap" => new LadyLapParser(webDriver, requestHeaders, filenameScheme),
            "xasiat" => new XasiatParser(webDriver, requestHeaders, filenameScheme),
            "catbox" => new CatBoxParser(webDriver, requestHeaders, filenameScheme),
            "jrants" => new JRantsParser(webDriver, requestHeaders, filenameScheme),
            "sexbjcam" => new SexBjCamParser(webDriver, requestHeaders, filenameScheme),
            "pornavhd" => new PornAvHdParser(webDriver, requestHeaders, filenameScheme),
            "knit" => new KnitParser(webDriver, requestHeaders, filenameScheme),
            "69tang" => new Six9TangParser(webDriver, requestHeaders, filenameScheme),
            "jieav" => new JieAvParser(webDriver, requestHeaders, filenameScheme),
            "hentaiclub" => new HentaiClubParser(webDriver, requestHeaders, filenameScheme),
            "avav19" => new Avav19Parser(webDriver, requestHeaders, filenameScheme),
            "booru" => new AllBooruParser(webDriver, requestHeaders, filenameScheme),
            "mangadex" => new MangaDexParser(webDriver, requestHeaders, filenameScheme),
            "cosblay" => new CosblayParser(webDriver, requestHeaders, filenameScheme),
            "kaizty" => new KaiztyParser(webDriver, requestHeaders, filenameScheme),
            "quatvn" => new QuatvnParser(webDriver, requestHeaders, filenameScheme),
            "mangapark" => new MangaParkParser(webDriver, requestHeaders),
            "noodlemagazine" => new NoodleMagazineParser(webDriver, requestHeaders),
            "spankbang" => new SpankBangParser(webDriver, requestHeaders, filenameScheme),
            "apcomics" => new ApComicsParser(webDriver, requestHeaders, filenameScheme),
            "3hentai" => new ThreeHentaiParser(webDriver, requestHeaders, filenameScheme),
            "3600000" => new Three600000Parser(webDriver, requestHeaders, filenameScheme),
            "asmhentai" => new AsmHentaiParser(webDriver, requestHeaders, filenameScheme),
            "ahottie" => new AHottieParser(webDriver, requestHeaders, filenameScheme),
            "baobua" => new BaobuaParser(webDriver, requestHeaders, filenameScheme),
            "foamgirl" => new FoamGirlParser(webDriver, requestHeaders, filenameScheme),
            "hentaiera" => new HentaiEraParser(webDriver, requestHeaders, filenameScheme),
            "hentaifox" => new HentaiFoxParser(webDriver, requestHeaders, filenameScheme),
            "hentaihand" => new HentaiHandParser(webDriver, requestHeaders, filenameScheme),
            "meijuntu" => new MeijuntuParser(webDriver, requestHeaders, filenameScheme),
            "pixiv" => new PixivParser(webDriver, requestHeaders, filenameScheme),
            "fcww0" => new Fcww0Parser(webDriver, requestHeaders, filenameScheme),
            "xsnvshen" => new XsnvshenParser(webDriver, requestHeaders, filenameScheme),
            "06se" => new Zero6SeParser(webDriver, requestHeaders, filenameScheme),
            "meirentu" => new MeirentuParser(webDriver, requestHeaders, filenameScheme),
            "8se" => new EightSeParser(webDriver, requestHeaders, filenameScheme),
            "51cg1" => new Five1Cg1Parser(webDriver, requestHeaders, filenameScheme),
            "tubeasiancams" => new TubeAsianCamsParser(webDriver, requestHeaders, filenameScheme),
            "koreanbj" => new KoreanBjParser(webDriver, requestHeaders, filenameScheme),
            "kbjfan" => new KbjFanParser(webDriver, requestHeaders, filenameScheme),
            "shameless" => new ShamelessParser(webDriver, requestHeaders, filenameScheme),
            "pussyspace" => new PussySpaceParser(webDriver, requestHeaders, filenameScheme),
            "videomonstr" => new VideoMonstrParser(webDriver, requestHeaders, filenameScheme),
            "pornoxo" => new PornOxoParser(webDriver, requestHeaders, filenameScheme),
            "porndr" => new PornDrParser(webDriver, requestHeaders, filenameScheme),
            "xhamster" => new XHamsterParser(webDriver, requestHeaders, filenameScheme),
            "abxxx" => new AbxxxParser(webDriver, requestHeaders, filenameScheme),
            //"love4porn" => new Love4PornParser(webDriver, requestHeaders, filenameScheme),
            "xvideos" => new XVideosParser(webDriver, requestHeaders, filenameScheme),
            "asianviralhub" => new AsianViralHubParser(webDriver, requestHeaders, filenameScheme),
            "hdzog" => new HdzogParser(webDriver, requestHeaders, filenameScheme),
            "pornzog" => new PornzogParser(webDriver, requestHeaders, filenameScheme),
            _ => throw new RipperException($"Site not supported: {siteName}")
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
    ///     
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected async Task<RipInfo> GenericBabesHtmlParser(string dirNameXpath, string imageContainerXpath)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow(dirNameXpath)
                          .InnerText;
        var images = soup.SelectNodesOrThrow(imageContainerXpath)
                         .SelectMany(im => im.SelectNodesOrThrow(".//img"))
                         .Select(img => Protocol + img.GetSrc().Remove("tn_"))
                         .Select(dummy => (StringImageLinkWrapper)dummy)
                         .ToList();

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
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
        var dirName = soup.SelectSingleNodeOrThrow("//img[@title='Click To Enlarge!']")
                          .GetAttributeValue("alt")
                          .Split(" ")
                          .TakeWhile(s => s != "-")
                          .Join(" ");
        var images = soup.SelectNodesOrThrow("//div[@class='gallery_thumb']")
                         .Select(img => Protocol + img.SelectSingleNodeOrThrow(".//img").GetSrc().Remove("tn_"))
                         .ToStringImageLinkWrapperList();

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> GenericHtmlParserHelper2()
    {
        var soup = await Soupify();
        var imageList = soup.SelectSingleNodeOrThrow("//ul[@class='list-gallery static css has-data']")
                            .SelectNodesOrThrow(".//a");
        var images = imageList.Select(image => image.GetHref())
                              .Select(dummy => (StringImageLinkWrapper)dummy)
                              .ToList();
        var dirName = imageList[0].SelectSingleNodeOrThrow(".//img")
                                  .GetAttributeValue("alt");

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> GenericHtmlParserHelper3()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//header[@id='top']").SelectSingleNodeOrThrow(".//h1").InnerText;
        var images = soup
                    .SelectSingleNodeOrThrow(
                         "//ul[contains(@class, 'list-gallery') and contains(@class, 'static') and contains(@class, 'css')]")
                    .SelectNodesOrThrow(".//a")
                    .Select(img => img.GetHref())
                    .Select(dummy => (StringImageLinkWrapper)dummy)
                    .ToList();

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
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

    protected async Task<HtmlNode> Soupify(int delay = 0, LazyLoadArgs? lazyLoadArgs = null, string xpath = "",
                                           int xpathTimout = 10)
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
                                           string xpath = "", bool urlString = true, ICookieJar? cookies = null,
                                           int xpathTimout = 10)
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

        return await Soupify(delay: delay, lazyLoadArgs: lazyLoadArgs, xpath: xpath, xpathTimout: xpathTimout);
    }

    protected static async Task<HtmlNode> Soupify(HttpResponseMessage response)
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
    protected async Task<string?> WaitForElement(string xpath, float delay = 0.1f, float timeout = 10)
    {
        var timeoutSpan = TimeSpan.FromSeconds(timeout);
        var startTime = DateTime.Now;
        var found = Driver.FindElements(By.XPath(xpath));
        while (found.Count == 0)
        {
            await Task.Delay((int)(delay * 1000));
            var currTime = DateTime.Now;
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (timeout == -1)
            {
                continue; // No timeout, keep waiting
            }
            
            if (currTime - startTime >= timeoutSpan)
            {
                return null;
            }
        }

        var foundElement = found[0];
        return foundElement.TagName;
    }

    /// <summary>
    ///     Close all tabs that do not contain the specified URL match.
    /// </summary>
    /// <param name="urlMatch">The substring that should be present in the URL of the tabs to keep open.</param>
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
        var solution = await Solve(regenerateSessionOnFailure, cookies);
        var cookieJar = Driver.GetCookieJar();
        foreach (var cookie in solution.Cookies)
        {
            Log.Debug("Adding cookie: {@Cookie}", cookie);
            var seleniumCookie = cookie.ToSeleniumCookie();
            cookieJar.SetCookie(seleniumCookie);
        }

        return await Soupify(solution);
    }

    protected async Task<HtmlNode> SolveParse(bool regenerateSessionOnFailure = false,
                                              List<Dictionary<string, string>>? cookies = null)
    {
        var solution = await Solve(regenerateSessionOnFailure, cookies);
        #if DEBUG
        await File.WriteAllTextAsync("test-solver.html", solution.Response);
        Log.Debug("User-Agent: {UserAgent}", solution.UserAgent);
        #endif
        return await Soupify(solution);
    }

    private async Task<Solution> Solve(bool regenerateSessionOnFailure = false,
                                       List<Dictionary<string, string>>? cookies = null)
    {
        if (!NicheImageRipper.AvailableFeatures.HasFlag(ExternalFeatureSupport.FlareSolverr))
        {
            throw new FeatureNotAvailableException(ExternalFeatureSupport.FlareSolverr);
        }

        // Safety: Solution will not be null unless all attempts fail in which case an exception is thrown.
        Solution solution = null!;
        for(var i = 0; i < 4; i++)
        {
            try
            {
                Log.Debug("Attempting to get site solution for {CurrentUrl} (Attempt {Attempt})", CurrentUrl, i + 1);
                solution = await FlareSolverrManager.GetSiteSolution(CurrentUrl, cookies);
                break;
            }
            catch (FailedToGetSolutionException)
            {
                if (i == 3)
                {
                    throw;
                }

                await Sleep(250);
                Log.Warning("Failed to get site solution for {CurrentUrl}, retrying...", CurrentUrl);
            }
        }
        
        return solution;
    }

    protected static Task JitterSleep(int min = 250, int max = 2500)
    {
        var jitter = Random.Shared.Next(min, max);
        return Task.Delay(jitter);
    }
    
    protected static Task Sleep(int milliseconds)
    {
        return Task.Delay(milliseconds);
    }

    protected async Task<(T, BiDi)> ConfigureNetworkCapture<T>() where T : PlaylistCapturer, new()
    {
        var capturer = new T();
        var bidi = await Driver.AsBiDiAsync();
        await bidi.Network.OnResponseCompletedAsync(capturer.CaptureHook);
        return (capturer, bidi);
    }

    protected static Dictionary<string, List<string>> CreateExternalLinkDict()
    {
        var externalLinks = new Dictionary<string, List<string>>();
        foreach (var site in EXTERNAL_SITES)
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

    protected static bool UrlCanBeParsed(string url)
    {
        return !string.IsNullOrEmpty(url) && EXTERNAL_SITES.Any(url.Contains);
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
        var currHeight = (long)(Driver.ExecuteScript("return window.pageYOffset") ?? 0);
        var scrollScript = $"window.scrollBy({{top: {currHeight + distance}, left: 0, behavior: 'smooth'}});";
        Driver.ExecuteScript(scrollScript);
    }

    protected void ScrollToTop()
    {
        Driver.ExecuteScript("window.scrollTo(0, 0);");
    }

    protected void WaitForPlaylist(PlaylistCapturer capturer, Action<List<string>> callback)
    {
        var i = 0;
        while (true)
        {
            var links = capturer.GetNewVideoLinks();
            if (links.Count == 0)
            {
                i++;
                if (i % 4 == 3)
                {
                    Driver.Refresh();
                }

                continue;
            }
    
            callback(links);
            break;
        }
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
            if (data.Urls.Count == 0)
            {
                Log.Error("No URLs found for {SiteName}Parse", SiteName);
            }
            else
            {
                Log.Debug("Referer: {Referer}", data.Urls[0].Referer);
            }
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
        catch (Exception e)
        {
            Log.Error(e, "Error occurred while testing {SiteName}Parse", SiteName);
            await File.WriteAllTextAsync("test.html", Driver.PageSource);
            Driver.TakeDebugScreenshot();
            Driver.DumpCookies();
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
        Log.Debug("Parser: {ParserName}", className);
        var classType = Assembly.GetExecutingAssembly()
                                .GetTypes()
                                .FirstOrDefault(t =>
                                     string.Equals(t.Name, className, StringComparison.OrdinalIgnoreCase));
        if (classType is not null)
        {
            var ripper =
                (HtmlParser)Activator.CreateInstance(classType, WebDriver, RequestHeaders, FilenameScheme)!;
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
        else if (siteName.Contains("18kami"))
        {
            siteName = siteName.Replace("18kami", "EighteenKami");
        }

        if (siteName[0] >= '0' && siteName[0] <= '9')
        {
            siteName = NumberToWord(siteName[0]) + char.ToUpper(siteName[1]) + siteName[2..];
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