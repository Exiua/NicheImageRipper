using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;

public class EHentaiParser : TimeSensitiveHtmlParser, IHtmlParser, IMultiSiteHtmlParser, IUrlNormalizingHtmlParser
{
    public static string ParserName => "e-hentai";
    public static string[] SupportedUrls => ["https://e-hentai.org/", "https://exhentai.org/"];
    public static string[] AdditionalParserNames { get; } = ["exhentai"];
    public static (string From, string To)[] UrlReplacements { get; } = [("exhentai.org", "e-hentai.org")];


    protected override int MaxEntriesPerBatch => 250;
    protected override string ParserKey => ParserName;
    protected override bool SupportsPartialSave => false;
    protected override bool RequiresLogin => true;

    public EHentaiParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                         FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<EHentaiParser>(filenameScheme))
    {
    }

    protected override async Task<bool> SiteLoginHelper(CancellationToken cancellationToken = default)
    {
        const string loginUrl = "https://forums.e-hentai.org/index.php?act=Login&CODE=00";
        var (username, password) = Config.Logins.EHentai;
        var currentUrl = CurrentUrl;
        CurrentUrl = loginUrl;
        Driver.FindElement(By.XPath("//input[@name='UserName']")).SendKeys(username);
        Driver.FindElement(By.XPath("//input[@name='PassWord']")).SendKeys(password);
        Driver.FindElement(By.XPath("//input[@name='submit']")).Click();
        while (CurrentUrl == loginUrl)
        {
            await Sleep(1000, cancellationToken);
        }

        CurrentUrl = "https://exhentai.org/";
        await Sleep(2500, cancellationToken);
        CurrentUrl = currentUrl;
        return true;
    }

    /// <summary>
    ///     Parses the HTML for e-hentai.org and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        await SiteLogin(cancellationToken);
        CurrentUrl = CurrentUrl.Replace("e-hentai.org", "exhentai.org");
        var imageLinks = TimeSensitiveParserStateManager.GetLinks(ParserKey, GivenUrl);
        string dirName;
        if (imageLinks is null)
        {
            var soup = await Soupify(cancellationToken: cancellationToken);
            dirName = soup.SelectSingleNodeOrThrow("//h1[@id='gn']").InnerText;
            imageLinks = [];
            await ExtractImageLinks(soup, imageLinks, cancellationToken);
            TimeSensitiveParserStateManager.StoreLinks(ParserKey, GivenUrl, imageLinks);
        }
        else
        {
            var soup = await Soupify(cancellationToken: cancellationToken);
            dirName = soup.SelectSingleNodeOrThrow("//h1[@id='gn']").InnerText;
        }

        Logger.Debug("Found {count} image links", imageLinks.Count);
        // Links to the images themselves
        var imageUrls = new List<string>();
        foreach (var (i, link)in imageLinks.Enumerate())
        {
            // Done to prevent taking too long getting links to the point where the links have expired
            // Also, E-Hentai seems to hang for too long when too many requests are made in a short period of time
            if (i >= MaxEntriesPerBatch)
            {
                Logger.Information("Skipping image {i} of {count}", i + 1, imageLinks.Count);
                imageUrls.Add("");
            }
            else
            {
                Logger.Information("Parsing image {i} of {count}", i + 1, imageLinks.Count);
                var img = await GetImageLink(link, cancellationToken);
                imageUrls.Add(img);
                await Sleep(1000, cancellationToken);
            }
        }

        var images = new List<FileLink>(imageUrls.Count);
        foreach (var (i, link)in imageUrls.Enumerate())
        {
            images.Add(link == "" ? FileLink.Invalid : FileLink.Create(link, FilenameScheme, i));
        }

        return RipInfo.GenerateWithInvalid(images, dirName, FilenameScheme);
    }

    private async Task<string> GetImageLink(string link, CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(link, cancellationToken: cancellationToken);
        var img = soup.SelectSingleNodeOrThrow("//img[@id='img']").GetSrc();
        return img;
    }

    protected override Task<string> UpdateLink(string link, CancellationToken cancellationToken = default)
    {
        return GetImageLink(link, cancellationToken);
    }

    private async Task ExtractImageLinks(HtmlNode soup, List<string> imageLinks,
                                         CancellationToken cancellationToken = default)
    {
        var pageCount = 1;
        while (true)
        {
            Logger.Information("Parsing page {pageCount}", pageCount);
            var imageTags = soup.SelectNodesSafe("//div[@id='gdt']/a").GetHrefs();
            if (imageTags.Count == 0)
            {
                Logger.Warning("No image links found on page {pageCount}. Stopping parsing.", pageCount);
                break;
            }

            imageLinks.AddRange(imageTags);
            var nextPage = soup.SelectSingleNodeOrThrow("//table[@class='ptb']").SelectNodesSafe(".//a").Last()
                               .GetHref();
            if (nextPage == CurrentUrl)
            {
                break;
            }

            await Sleep(1000, cancellationToken);
            try
            {
                pageCount += 1;
                CurrentUrl = nextPage;
            }
            catch (WebDriverTimeoutException)
            {
                Logger.Warning("Timed out. Sleeping for 10 seconds before retrying...");
                await Sleep(10000, cancellationToken);
                CurrentUrl = nextPage;
            }

            soup = await Soupify(cancellationToken: cancellationToken);
        }
    }
}