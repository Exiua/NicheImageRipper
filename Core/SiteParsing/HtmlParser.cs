using System.Reflection;
using Common.ExtensionMethods;
using Core.Configuration;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.Managers;
using Core.FileDownloading;
using Core.SiteParsing.HtmlParsers;
using Core.SiteParsing.VideoCapturers;
using Core.Utility;
using FlareSolverrIntegration.Responses;
using HtmlAgilityPack;
using OpenQA.Selenium;
using OpenQA.Selenium.BiDi;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Manager;
using Serilog;
using Serilog.Events;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing;

public abstract class HtmlParser : IDisposable
{
    protected const string Protocol = "https:";

    protected static readonly string[] ExternalSites =
        ["drive.google.com", "mega.nz", "mediafire.com", "sendvid.com", "dropbox.com", "youtube.com"];

    protected static GeneralConfig Config => Configuration.Config.Instance;
    protected static TokenManager TokenManager => TokenManager.Instance;

    protected WebDriver WebDriver { get; }
    public bool Interrupted { get; set; }
    private string SiteName { get; set; }
    public float SleepTime { get; set; }
    public float Jitter { get; set; }
    public int RetryCount { get; set; } = 4;
    protected string GivenUrl { get; private set; }
    protected FilenameScheme FilenameScheme { get; }
    protected Dictionary<string, string> RequestHeaders { get; }
    protected ApiClientManager ApiClientManager { get; }
    protected ILogger Logger { get; init; }

    protected FirefoxDriver Driver => WebDriver.Driver;

    protected string CurrentUrl
    {
        get => Driver.Url;
        set => Driver.Url = value;
    }

    protected static bool Debugging { get; set; }
    protected static FlareSolverrManager FlareSolverrManager => NicheImageRipper.FlareSolverrManager;
    protected static string UserAgent => Config.UserAgent;

    protected HtmlParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                         FilenameScheme filenameScheme = FilenameScheme.Original)
    {
        WebDriver = driver;
        ApiClientManager = clientManager;
        RequestHeaders = requestHeaders;
        FilenameScheme = filenameScheme;
        Interrupted = false;
        SiteName = "";
        SleepTime = 0.2f;
        Jitter = 0.5f;
        GivenUrl = "";
        Logger = Log.ForContext<HtmlParser>();
    }

    public async Task<RipInfo> ParseSite(string url, CancellationToken cancellationToken = default)
    {
        Logger.Debug("Parsing {Url}", url);
        url = url.Replace("members.", "www.") // For HAnime
                 .Replace("exhentai.org", "e-hentai.org"); // Need to go through e-hentai first for cookies
        GivenUrl = url;
        (SiteName, SleepTime) = UrlUtility.SiteCheck(GivenUrl, RequestHeaders);
        // e-hentai image links expire too quickly, so we need to parse the site every time
        if (File.Exists("partial.json") && (SiteName != "e-hentai" && SiteName != "exhentai"))
        {
            Logger.Debug("Partial save file found");
            var saveData = ReadPartialSave();
            if (saveData.TryGetValue(url, out var value))
            {
                Logger.Debug("Partial save found for {Url}", url);
                RequestHeaders["cookie"] = value.Cookies;
                RequestHeaders["referer"] = value.Referer;
                Interrupted = true;
                return value.RipInfo;
            }
        }
        else
        {
            File.Delete(ImageRipper.RipIndexPath); // Not valid when no partial save
        }

        Logger.Debug("No partial save found for site; Parsing site");
        if (SiteName != "booru")
        {
            CurrentUrl = url;
        }

        // Logger.Debug("Getting parser for {SiteName}", SiteName);
        // var siteParser = GetParser(SiteName);
        for (var attempt = 0; attempt < RetryCount; attempt++)
        {
            try
            {
                Logger.Debug("Executing parser for {SiteName}", SiteName);
                var siteInfo = await Parse(cancellationToken);
                Logger.Debug("Saving partial save for {Url}", url);
                WritePartialSave(siteInfo, url);
                //pickle.dump(self.driver.get_cookies(), open("cookies.pkl", "wb"))
                return siteInfo;
            }
            catch (WebDriverException e)
            {
                if (attempt < RetryCount - 1)
                {
                    Logger.Warning(e, "Attempt {Attempt} failed due to WebDriver, retrying...", attempt + 1);
                    await Sleep(250, cancellationToken);
                    continue;
                }

                await CleanupWhenFailed(e, cancellationToken);
                throw;
            }
            catch (Exception e)
            {
                await CleanupWhenFailed(e, cancellationToken);
                throw;
            }
        }

        // Can only reach here if RetryCount is less than 1 as the loop would have returned or thrown
        throw new RipperException("Retry count cannot be less than 1");
    }

    private async Task CleanupWhenFailed(Exception e, CancellationToken cancellationToken = default)
    {
        Driver.SwitchTo().DefaultContent();
        Logger.Error(e, "Failed to parse {CurrentUrl}", CurrentUrl);
        #if DEBUG
        await File.WriteAllTextAsync("test.html", Driver.PageSource, cancellationToken);
        Driver.TakeDebugScreenshot();
        #endif
    }

    public static HtmlParser GetParser(string siteName, WebDriver webDriver, ApiClientManager clientManager,
                                       Dictionary<string, string> requestHeaders,
                                       FilenameScheme filenameScheme = FilenameScheme.Original)
    {
        return HtmlParserFactory.Create(siteName, webDriver, clientManager, requestHeaders, filenameScheme);
    }

    // Working on phasing this out into a factory pattern with auto-registration
    public static HtmlParser GetParser2(string siteName, WebDriver webDriver, ApiClientManager clientManager,
                                        Dictionary<string, string> requestHeaders,
                                        FilenameScheme filenameScheme = FilenameScheme.Original)
    {
        return siteName switch
        {
            "imhentai" => new ImhentaiParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "kemono" => new KemonoParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "coomer" => new CoomerParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "sankakucomplex" => new SankakuComplexParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "omegascans" => new OmegaScansParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "redgifs" => new RedGifsParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "rule34" => new Rule34Parser(webDriver, clientManager, requestHeaders, filenameScheme),
            "gelbooru" => new GelbooruParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "danbooru" => new DanbooruParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "google" => new GoogleParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "dropbox" => new DropboxParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "imgur" => new ImgurParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "newgrounds" => new NewgroundsParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "wnacg" => new WnacgParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "arca" => new ArcaParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "babecentrum" => new BabeCentrumParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "babeimpact" => new BabeImpactParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "babeuniversum" => new BabeUniversumParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "babesandbitches" => new BabesAndBitchesParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "babesandgirls" => new BabesAndGirlsParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "babesaround" => new BabesAroundParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "babesbang" => new BabesBangParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "babesinporn" => new BabesInPornParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "babesmachine" => new BabesMachineParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "bestprettygirl" => new BestPrettyGirlParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "bitchesgirls" => new BitchesGirlsParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "bunkr" => new BunkrParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "buondua" => new BuonduaParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "bustybloom" => new BustyBloomParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "camwhores" => new CamwhoresParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "cherrynudes" => new CherryNudesParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "chickteases" => new ChickTeasesParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "cool18" => new Cool18Parser(webDriver, clientManager, requestHeaders, filenameScheme),
            "cutegirlporn" => new CuteGirlPornParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "cyberdrop" => new CyberDropParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "decorativemodels" => new DecorativeModelsParser(webDriver, clientManager, requestHeaders, filenameScheme),
            //DeviantArt
            "dirtyyoungbitches" =>
                new DirtyYoungBitchesParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "e-hentai" or "exhentai" => new EHentaiParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "eahentai" => new EahentaiParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "8boobs" => new EightBoobsParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "8muses" => new EightMusesParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "elitebabes" => new EliteBabesParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "erosberry" => new ErosBerryParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "erohive" => new EroHiveParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "erome" => new EroMeParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "erothots" => new EroThotsParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "everia" => new EveriaParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "exgirlfriendmarket" => new ExGirlFriendMarketParser(webDriver, clientManager, requestHeaders,
                filenameScheme),
            "fapello" => new FapelloParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "faponic" => new FaponicParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "f5girls" => new F5GirlsParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "femjoyhunter" => new FemJoyHunterParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "flickr" => new FlickrParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "foxhq" => new FoxHqParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "ftvhunter" => new FtvHunterParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "ggoorr" => new GgoorrParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "girlsofdesire" => new GirlsOfDesireParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "girlsreleased" => new GirlsReleasedParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "glam0ur" => new Glam0urParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "grabpussy" => new GrabPussyParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "gyrls" => new GyrlsParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "hegrehunter" => new HegreHunterParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "hentai-cosplays" => new HentaiCosplaysParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "hentairox" => new HentaiRoxParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "hustlebootytemptats" => new HustleBootyTempTatsParser(webDriver, clientManager, requestHeaders,
                filenameScheme),
            "hotgirl" => new HotGirlParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "hotstunners" => new HotStunnersParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "hottystop" => new HottyStopParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "100bucksbabes" => new HundredBucksBabesParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "imgbox" => new ImgBoxParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "influencersgonewild" => new InfluencersGoneWildParser(webDriver, clientManager, requestHeaders,
                filenameScheme),
            "inven" => new InvenParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "jkforum" => new JkForumParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "join2babes" => new Join2BabesParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "joymiihub" => new JoyMiiHubParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "leakedbb" => new LeakedBbParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "livejasminbabes" => new LiveJasminBabesParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "luscious" => new LusciousParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "mainbabes" => new MainBabesParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "manganato" or "chapmanganato" => new ManganatoParser(webDriver, clientManager, requestHeaders,
                filenameScheme),
            "metarthunter" => new MetArtHunterParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "morazzia" => new MorazziaParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "myhentaigallery" => new MyHentaiGalleryParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "micmicdoll" => new MicMicDollParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "nakedgirls" => new NakedGirlsParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "nhentai" => new NHentaiParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "nightdreambabe" => new NightDreamBabeParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "nijie" => new NijieParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "novoglam" => new NovoGlamParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "novohot" => new NovoHotParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "novoporn" => new NovoPornParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "nudebird" => new NudeBirdParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "nudity911" => new Nudity911Parser(webDriver, clientManager, requestHeaders, filenameScheme),
            "pbabes" => new PBabesParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "pixeldrain" => new PixelDrainParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "pmatehunter" => new PMateHunterParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "porn3dx" => new Porn3dxParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "pornhub" => new PornhubParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "putmega" => new PutMegaParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "rabbitsfun" => new RabbitsFunParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "redpornblog" => new RedPornBlogParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "rossoporn" => new RossoPornParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "sensualgirls" => new SensualGirlsParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "sexhd" => new SexHdParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "sexyaporno" => new SexyAPornoParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "sexybabesart" => new SexyBabesArtParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "sexykittenporn" => new SexyKittenPornParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "sexynakeds" => new SexyNakedsParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "sfmcompile" => new SfmCompileParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "silkengirl" => new SilkenGirlParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "simply-cosplay" => new SimplyCosplayParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "sxchinesegirlz01" => new SxChineseGirlz01Parser(webDriver, clientManager, requestHeaders, filenameScheme),
            "pleasuregirl" => new PleasureGirlParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "theomegaproject" => new TheOmegaProjectParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "thothub" => new ThothubParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "titsintops" => new TitsInTopsParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "toonily" => new ToonilyParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "tsumino" => new TsuminoParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "twitter" or "x" => new TwitterParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "xcancel" => new XCancelParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "wantedbabes" => new WantedBabesParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "xarthunter" => new XArtHunterParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "xmissy" => new XMissyParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "yande" => new YandeParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "18kami" => new EighteenKamiParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "cup2d" => new Cup2DParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "5ge" => new FiveGeParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "japaneseasmr" => new JapaneseAsmrParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "spacemiss" => new SpaceMissParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "xiuren" => new XiurenParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "xchina" => new XChinaParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "gofile" => new GoFileParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "jpg5" => new Jpg5Parser(webDriver, clientManager, requestHeaders, filenameScheme),
            "simpcity" => new SimpCityParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "rule34video" => new Rule34VideoParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "av19a" => new Av19aParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "eporner" => new EpornerParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "cgcosplay" => new CgCosplayParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "4khd" => new FourKHdParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "cosplay69" => new Cosplay69Parser(webDriver, clientManager, requestHeaders, filenameScheme),
            "nlegs" => new NLegsParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "ladylap" => new LadyLapParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "xasiat" => new XasiatParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "catbox" => new CatBoxParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "jrants" => new JRantsParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "sexbjcam" => new SexBjCamParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "pornavhd" => new PornAvHdParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "knit" => new KnitParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "69tang" => new Six9TangParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "jieav" => new JieAvParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "hentaiclub" => new HentaiClubParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "avav19" => new Avav19Parser(webDriver, clientManager, requestHeaders, filenameScheme),
            "booru" => new AllBooruParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "mangadex" => new MangaDexParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "cosblay" => new CosblayParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "kaizty" => new KaiztyParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "quatvn" => new QuatvnParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "mangapark" => new MangaParkParser(webDriver, clientManager, requestHeaders),
            "noodlemagazine" => new NoodleMagazineParser(webDriver, clientManager, requestHeaders),
            "spankbang" => new SpankBangParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "apcomics" => new ApComicsParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "3hentai" => new ThreeHentaiParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "3600000" => new Three600000Parser(webDriver, clientManager, requestHeaders, filenameScheme),
            "asmhentai" => new AsmHentaiParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "ahottie" => new AHottieParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "baobua" => new BaobuaParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "foamgirl" => new FoamGirlParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "hentaiera" => new HentaiEraParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "hentaifox" => new HentaiFoxParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "hentaihand" => new HentaiHandParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "meijuntu" => new MeijuntuParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "pixiv" => new PixivParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "fcww0" => new Fcww0Parser(webDriver, clientManager, requestHeaders, filenameScheme),
            "xsnvshen" => new XsnvshenParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "06se" => new Zero6SeParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "meirentu" => new MeirentuParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "8se" => new EightSeParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "51cg1" => new Five1Cg1Parser(webDriver, clientManager, requestHeaders, filenameScheme),
            "tubeasiancams" => new TubeAsianCamsParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "koreanbj" => new KoreanBjParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "kbjfan" => new KbjFanParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "shameless" => new ShamelessParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "pussyspace" => new PussySpaceParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "videomonstr" => new VideoMonstrParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "pornoxo" => new PornOxoParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "porndr" => new PornDrParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "xhamster" => new XHamsterParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "abxxx" => new AbxxxParser(webDriver, clientManager, requestHeaders, filenameScheme),
            //"love4porn" => new Love4PornParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "xvideos" => new XVideosParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "asianviralhub" => new AsianViralHubParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "hdzog" => new HdzogParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "pornzog" => new PornzogParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "x-x-x" => new XxxTubeParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "privatehomeclips" => new PrivateHomeClipsParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "archivebate" => new ArchivebateParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "e621" => new E621Parser(webDriver, clientManager, requestHeaders, filenameScheme),
            "missav123" => new MissAv123Parser(webDriver, clientManager, requestHeaders, filenameScheme),
            "porncomics18" => new PornComics18Parser(webDriver, clientManager, requestHeaders, filenameScheme),
            "porncomic" => new PornComicParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "hmvmania" => new HmvManiaParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "pmvhaven" => new PmvHavenParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "youtube" => new YoutubeParser(webDriver, clientManager, requestHeaders, filenameScheme),
            "hanime1" => new Hanime1Parser(webDriver, clientManager, requestHeaders, filenameScheme),
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

    // TODO: Make private and call from ParseSite so children only need to implement SiteLoginHelper instead of worrying
    //  about calling SiteLogin as well
    //  Only issue is with GoFileParser/ParameterizedHtmlParser where CurrentUrl may need to be set before login
    protected Task<bool> SiteLogin(CancellationToken cancellationToken = default)
    {
        Logger.Debug("Checking if already logged in to {SiteName}", SiteName);
        if (IsLoggedInToSite(SiteName))
        {
            Logger.Debug("Already logged in to {SiteName}", SiteName);
            return Task.FromResult(true);
        }

        Logger.Debug("Logging in to {SiteName}", SiteName);
        var loginTask = SiteLoginHelper(cancellationToken);

        return loginTask.ContinueWith(task =>
        {
            WebDriver.SiteLoginStatus[SiteName] = task.Result;
            Logger.Debug("Logged in to {SiteName}: {Result}", SiteName, task.Result);
            return task.Result;
        }, cancellationToken);
    }

    protected virtual Task<bool> SiteLoginHelper(CancellationToken cancellationToken = default)
    {
        throw new Exception("Site authentication not implemented");
    }

    private bool IsLoggedInToSite(string siteName)
    {
        var siteLoginStatus = WebDriver.SiteLoginStatus;
        return !siteLoginStatus.TryAdd(siteName, false) && siteLoginStatus[siteName];
    }

    protected abstract Task<RipInfo> Parse(CancellationToken cancellationToken = default);

    #region Generic Site Parsers

    /// <summary>
    ///     
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected async Task<RipInfo> GenericBabesHtmlParser(string dirNameXpath, string imageContainerXpath, CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow(dirNameXpath)
                          .InnerText;
        var images = soup.SelectNodesOrThrow(imageContainerXpath)
                         .SelectMany(im => im.SelectNodesOrThrow(".//img"))
                         .Select(img => Protocol + img.GetSrc().Remove("tn_"))
                         .Select(dummy => (StringImageLinkWrapper)dummy)
                         .ToList();

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    protected Task<RipInfo> GenericHtmlParser(string siteName, CancellationToken cancellationToken = default)
    {
        return siteName switch
        {
            "bustybloom" or "sexyaporno" => GenericHtmlParserHelper1(cancellationToken),
            "elitebabes" => GenericHtmlParserHelper2(cancellationToken),
            "femjoyhunter" or "ftvhunter" or "hegrehunter" or "joymiihub"
                or "metarthunter" or "pmatehunter" or "xarthunter" => GenericHtmlParserHelper3(cancellationToken),
            _ => throw new RipperException($"Invalid site name: {siteName}")
        };
    }

    /// <summary>
    ///     
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> GenericHtmlParserHelper1(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
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
    private async Task<RipInfo> GenericHtmlParserHelper2(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
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
    private async Task<RipInfo> GenericHtmlParserHelper3(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
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

    /// <summary>
    ///     Convert current page into an HtmlNode object
    /// </summary>
    /// <param name="delay">How long to wait after loading the page (in milliseconds) before parsing</param>
    /// <param name="lazyLoadArgs">Arguments for lazy loading elements on the page</param>
    /// <param name="xpath">XPath of an element to wait for before parsing</param>
    /// <param name="xpathTimout">Timeout (in seconds) for waiting for the XPath element</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>>Parsed HtmlNode object</returns>
    protected async Task<HtmlNode> Soupify(int delay = 0, LazyLoadArgs? lazyLoadArgs = null, string xpath = "",
                                           int xpathTimout = 10, CancellationToken cancellationToken = default)
    {
        if (delay > 0)
        {
            await Task.Delay(delay, cancellationToken);
        }

        if (xpath != "")
        {
            await WaitForElement(xpath, timeout: xpathTimout, cancellationToken: cancellationToken);
        }

        if (lazyLoadArgs is not null)
        {
            await LazyLoad(lazyLoadArgs, cancellationToken);

        }

        var doc = new HtmlDocument();
        doc.LoadHtml(Driver.PageSource);
        return doc.DocumentNode;
    }

    /// <summary>
    ///     Convert input into an HtmlNode object
    /// </summary>
    /// <param name="url">URL or HTML string. If a url is provided, the driver will navigate to it first, before parsing the page.</param>
    /// <param name="delay">How long to wait after loading the page (in milliseconds) before parsing</param>
    /// <param name="lazyLoadArgs">Arguments for lazy loading elements on the page</param>
    /// <param name="xpath">XPath of an element to wait for before parsing</param>
    /// <param name="urlString">Indicates whether the 'url' parameter is a URL (true) or an HTML string (false)</param>
    /// <param name="cookies">Cookies to add before loading the page</param>
    /// <param name="xpathTimout">Timeout (in seconds) for waiting for the XPath element</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>>Parsed HtmlNode object</returns>
    protected async Task<HtmlNode> Soupify(string url, int delay = 0, LazyLoadArgs? lazyLoadArgs = null,
                                           string xpath = "", bool urlString = true, ICookieJar? cookies = null,
                                           int xpathTimout = 10, CancellationToken cancellationToken = default)
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

        return await Soupify(delay: delay, lazyLoadArgs: lazyLoadArgs, xpath: xpath, xpathTimout: xpathTimout, cancellationToken: cancellationToken);
    }

    /// <summary>
    ///     Convert HttpResponseMessage content into an HtmlNode object
    /// </summary>
    /// <param name="response">HttpResponseMessage to parse</param>
    /// <returns>>Parsed HtmlNode object</returns>
    protected static async Task<HtmlNode> Soupify(HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(content);
        return htmlDocument.DocumentNode;
    }

    protected async Task<HtmlNode> Soupify(CSWebDriverClient.Models.Responses.BaseResponse baseResponse, CancellationToken cancellationToken = default)
    {
        return baseResponse switch
        {
            CSWebDriverClient.Models.Responses.ErrorResponse errorResponse => 
                errorResponse.Details is not null
                    ? throw new RipperException($"{errorResponse.Error}: {errorResponse.Details}")
                    : throw new RipperException(errorResponse.Error),
            CSWebDriverClient.Models.Responses.PageResponse pageResponse => await Soupify(pageResponse.Content, urlString: false, cancellationToken: cancellationToken),
            CSWebDriverClient.Models.Responses.GetNetworkUrlsResponse =>
                throw new RipperException("Incorrect response type: GetNetworkUrlsResponse"),
            _ => throw new RipperException($"Unknown response type: {baseResponse}")
        };
    }

    /// <summary>
    ///     Convert FlareSolverr Solution response into an HtmlNode object
    /// </summary>
    /// <param name="solution">FlareSolverr Solution to parse</param>
    /// <returns>>Parsed HtmlNode object</returns>
    private static Task<HtmlNode> Soupify(Solution solution, CancellationToken cancellationToken = default)
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
    /// <param name="timeout">Timeout (in seconds) for the wait (-1 for no timeout)</param>
    /// <returns>True if the element exists, false if the timeout is reached</returns>
    protected async Task<string?> WaitForElement(string xpath, float delay = 0.1f, float timeout = 10, CancellationToken cancellationToken = default)
    {
        var timeoutSpan = TimeSpan.FromSeconds(timeout);
        var startTime = DateTime.Now;
        var found = Driver.FindElements(By.XPath(xpath));
        while (found.Count == 0)
        {
            await Task.Delay((int)(delay * 1000), cancellationToken);
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
    /// <param name="cookieWhitelist">List of cookie names to retain from the existing session.</param>
    /// <param name="replaceUserAgent">Whether to replace the User-Agent header with the one provided by FlareSolverr.</param>
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
                                                        List<Dictionary<string, string>>? cookies = null,
                                                        List<string>? cookieWhitelist = null,
                                                        bool replaceUserAgent = false, CancellationToken cancellationToken = default)
    {
        var solution = await Solve(regenerateSessionOnFailure, cookies, cancellationToken);
        if (replaceUserAgent)
        {
            Logger.Debug("Replacing User-Agent with FlareSolverr provided User-Agent: {UserAgent}", solution.UserAgent);
            var currentUrl = CurrentUrl;
            WebDriver.RegenerateDriver(solution.UserAgent);
            CurrentUrl = currentUrl;
        }

        var cookieJar = Driver.GetCookieJar();
        foreach (var cookie in solution.Cookies.Where(cookie =>
                     cookieWhitelist is null || cookieWhitelist.Contains(cookie.Name)))
        {
            Logger.Debug("Adding cookie: {@Cookie}", cookie);
            var seleniumCookie = cookie.ToSeleniumCookie();
            cookieJar.SetCookie(seleniumCookie);
        }

        return await Soupify(solution, cancellationToken: cancellationToken);
    }

    protected async Task<HtmlNode> SolveParse(bool regenerateSessionOnFailure = false,
                                              List<Dictionary<string, string>>? cookies = null, CancellationToken cancellationToken = default)
    {
        var solution = await Solve(regenerateSessionOnFailure, cookies, cancellationToken);
        return await Soupify(solution, cancellationToken: cancellationToken);
    }

    private async Task<Solution> Solve(bool regenerateSessionOnFailure = false,
                                       List<Dictionary<string, string>>? cookies = null, CancellationToken cancellationToken = default)
    {
        if (!NicheImageRipper.AvailableFeatures.HasFlag(ExternalFeatureSupport.FlareSolverr))
        {
            throw new FeatureNotAvailableException(ExternalFeatureSupport.FlareSolverr);
        }

        // Safety: Solution will not be null unless all attempts fail in which case an exception is thrown.
        Solution solution = null!;
        for (var i = 0; i < 4; i++)
        {
            try
            {
                Logger.Debug("Attempting to get site solution for {CurrentUrl} (Attempt {Attempt})", CurrentUrl, i + 1);
                solution = await FlareSolverrManager.GetSiteSolution(CurrentUrl, cookies, cancellationToken);
                break;
            }
            catch (FailedToGetSolutionException)
            {
                if (i == 3)
                {
                    throw;
                }

                await Sleep(250, cancellationToken);
                Logger.Warning("Failed to get site solution for {CurrentUrl}, retrying...", CurrentUrl);
            }
        }

        #if DEBUG
        await File.WriteAllTextAsync("test-solver.html", solution.Response, cancellationToken);
        Logger.Debug("User-Agent: {UserAgent}", solution.UserAgent);
        #endif

        return solution;
    }

    protected static Task JitterSleep(int min = 250, int max = 2500, CancellationToken cancellationToken = default)
    {
        var jitter = Random.Shared.Next(min, max);
        return Task.Delay(jitter, cancellationToken);
    }

    protected static Task Sleep(int milliseconds, CancellationToken cancellationToken = default)
    {
        return Task.Delay(milliseconds, cancellationToken);
    }

    protected static async Task<T> RetryUntil<T>(Func<Task<T>> func, Func<T, bool> successCondition,
                                                 string errorMessage, int delay = 250, CancellationToken cancellationToken = default)
    {
        const int maxAttempts = 4;
        T value = default!;
        for (var i = 0; i < maxAttempts; i++)
        {
            value = await func();
            if (!successCondition(value))
            {
                if (i == maxAttempts - 1)
                {
                    throw new RipperException(errorMessage);
                }

                await Sleep(delay, cancellationToken);
                continue;
            }

            break;
        }

        return value;
    }

    protected static async Task<T> DeserializeCache<T>(string cachePath, Func<Task<T>> fetchFunc, CancellationToken cancellationToken = default) where T : class
    {
        T data;
        if (File.Exists(cachePath))
        {
            var temp = JsonUtility.Deserialize<T>(cachePath);
            if (temp is null)
            {
                data = await fetchFunc();
            }
            else
            {
                data = temp;
            }
        }
        else
        {
            data = await fetchFunc();
        }

        return data;
    }

    protected async Task<(T, IBiDi)> ConfigureNetworkCapture<T>(CancellationToken cancellationToken = default) where T : PlaylistCapturer, new()
    {
        var capturer = new T();
        var bidi = await Driver.AsBiDiAsync(cancellationToken: cancellationToken);
        await bidi.Network.OnResponseCompletedAsync(capturer.CaptureHook, cancellationToken: cancellationToken);
        return (capturer, bidi);
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
        return !string.IsNullOrEmpty(url) && ExternalSites.Any(url.Contains);
    }

    /// <summary>
    ///     Scrolls through the page to lazy load images
    /// </summary>
    /// <param name="args">Arguments for lazy loading</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    protected Task LazyLoad(LazyLoadArgs args, CancellationToken cancellationToken = default)
    {
        return args.StopElement is not null && Driver.TryFindElement(args.StopElement) is not null
            ? LazyLoad(args.StopElement, cancellationToken: cancellationToken)
            : LazyLoad(args.ScrollBy, args.Increment, args.ScrollPauseTime, args.ScrollBack, args.ReScroll, cancellationToken);
    }

    /// <summary>
    ///     Scroll through the page to lazy load images
    /// </summary>
    /// <param name="scrollBy">Whether to scroll through the page or instantly scroll to the bottom</param>
    /// <param name="increment">Distance to scroll by each iteration</param>
    /// <param name="scrollPauseTime">Seconds to wait between each scroll</param>
    /// <param name="scrollBack">Distance to scroll back by after reaching the bottom of the page</param>
    /// <param name="rescroll">Whether scrolling through the page again</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    protected async Task LazyLoad(bool scrollBy = false, int increment = 2500, int scrollPauseTime = 500,
                                  int scrollBack = 0, bool rescroll = false, CancellationToken cancellationToken = default)
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
            await Task.Delay(scrollPauseTime, cancellationToken);
            var newHeight = Convert.ToInt64(Driver.ExecuteScript(heightCheckScript));
            if (newHeight == lastHeight)
            {
                if (scrollBack > 0)
                {
                    for (var i = 0; i < scrollBack; i++)
                    {
                        Driver.ExecuteScript($"window.scrollBy({{top: {-increment}, left: 0, behavior: 'smooth'}});");
                        await Task.Delay(scrollPauseTime, cancellationToken);
                    }

                    await Task.Delay(scrollPauseTime, cancellationToken);
                }

                break;
            }

            lastHeight = newHeight;
        }
    }

    protected async Task LazyLoad(By elementToFind, int increment = 1250, int scrollPauseTime = 500, CancellationToken cancellationToken = default)
    {
        var scrollScript = $"window.scrollBy({{top: {increment}, left: 0, behavior: 'smooth'}});";
        while (true)
        {
            Driver.ExecuteScript(scrollScript);
            await Task.Delay(scrollPauseTime, cancellationToken);
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

    protected async Task WaitForPlaylist(PlaylistCapturer capturer, Action<List<string>> callback, CancellationToken cancellationToken = default)
    {
        var i = 0;
        while (true)
        {
            var links = capturer.GetNewVideoLinks();
            if (links.Count == 0)
            {
                i++;
                if (i % 50 == 49)
                {
                    Logger.Debug("No playlist links found yet, refreshing page...");
                    Driver.Refresh();
                }

                await Sleep(250, cancellationToken);
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

    public async Task<RipInfo> TestParse(string givenUrl, bool debug, bool printSite, CancellationToken cancellationToken = default)
    {
        try
        {
            OpenQA.Selenium.Internal.Logging.Log.SetLevel(
                typeof(OpenQA.Selenium.Remote.RemoteWebDriver),
                OpenQA.Selenium.Internal.Logging.LogEventLevel.Trace);
            OpenQA.Selenium.Internal.Logging.Log.SetLevel(
                typeof(SeleniumManager),
                OpenQA.Selenium.Internal.Logging.LogEventLevel.Trace);
            /*var options = InitializeOptions(debug);
            Driver = new FirefoxDriver(options);*/
            CurrentUrl = givenUrl.Replace("members.", "www.");
            SiteName = TestSiteCheck(givenUrl);

            Logger.Debug("Testing: {SiteName}Parse", SiteName);
            Logger.Debug("URL: {CurrentUrl}", CurrentUrl);
            var start = DateTime.Now;
            var data = await EvaluateParser(SiteName, cancellationToken);
            var end = DateTime.Now;
            if (data.Urls.Count == 0)
            {
                Logger.Error("No URLs found for {SiteName}Parse", SiteName);
            }
            else
            {
                Logger.Debug("Referer: {Referer}", data.Urls[0].Referer);
            }

            Logger.Debug("Time Elapsed: {TimeElapsed}", end - start);
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
            Logger.Error(e, "Error occurred while testing {SiteName}Parse", SiteName);
            await File.WriteAllTextAsync("test.html", Driver.PageSource, cancellationToken);
            Driver.TakeDebugScreenshot();
            Driver.DumpCookies();
            throw;
        }
        finally
        {
            if (printSite)
            {
                await File.WriteAllTextAsync("test.html", Driver.PageSource, cancellationToken);
            }

            //await FlareSolverrManager.DeleteSession();
        }
    }

    private Task<RipInfo> EvaluateParser(string siteName, CancellationToken cancellationToken = default)
    {
        siteName = TestSiteConverter(siteName);
        siteName = siteName[0].ToString().ToUpper() + siteName[1..];
        var className = $"{siteName}Parser";
        Logger.Debug("Parser: {ParserName}", className);
        var classType = Assembly.GetExecutingAssembly()
                                .GetTypes()
                                .FirstOrDefault(t =>
                                     string.Equals(t.Name, className, StringComparison.OrdinalIgnoreCase));
        if (classType is not null)
        {
            var ripper =
                (HtmlParser)Activator.CreateInstance(classType, WebDriver, ApiClientManager, RequestHeaders,
                    FilenameScheme)!;
            return ripper.Parse(cancellationToken);
        }

        // Handle the case where the method does not exist
        Logger.Error("Parser {ParserName} not found.", className);
        throw new InvalidOperationException();
    }

    private static string TestSiteConverter(string siteName)
    {
        if (siteName == "x")
        {
            return "twitter";
        }

        if (siteName == "booru")
        {
            return "allbooru";
        }

        if (siteName == "x-x-x")
        {
            return "xxxtube";
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

        DisposeInternal();

        GC.SuppressFinalize(this);
    }

    protected virtual void DisposeInternal()
    {
    }
}

public abstract class HtmlParser<T> : HtmlParser
    where T : HtmlParser<T>, IHtmlParser
{
    protected HtmlParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                         FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, filenameScheme)
    {
    }
}