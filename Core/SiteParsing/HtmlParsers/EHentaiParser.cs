using Common.ExtensionMethods;
using HtmlAgilityPack;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.Utility;
using OpenQA.Selenium;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class EHentaiParser : TimeSensitiveHtmlParser, IHtmlParser
{
    public static string ParserName => "e-hentai or exhentai";

    protected override string ImageLinksFileName => "ehentai.json";
    protected override int MaxEntriesPerBatch => 250;
    protected override string ParserKey => "ehentai";

    public EHentaiParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<EHentaiParser>(filenameScheme))
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
            await Sleep(1000);
        }
        
        CurrentUrl = "https://exhentai.org/";
        await Sleep(2500);
        CurrentUrl = currentUrl;

        return true;
    }

    /// <summary>
    ///     Parses the html for e-hentai.org and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        await SiteLogin();
        CurrentUrl = CurrentUrl.Replace("e-hentai.org", "exhentai.org"); // Redirect to exhentai
        var currentUrl = CurrentUrl;
        StoreLastLink(currentUrl);
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@id='gn']").InnerText;
        // Links to each image page
        var imageLinks = await GetImageLinks(soup, currentUrl);
        
        Logger.Debug("Found {count} image links", imageLinks.Count);
        
        var imageLinksSave = new Dictionary<string, List<string>>
        {
            [currentUrl] = imageLinks
        };
        
        JsonUtility.Serialize(ImageLinksFileName, imageLinksSave);
        
        // Links to the images themselves
        var imageUrls = new List<string>();
        foreach (var (i, link) in imageLinks.Enumerate())
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
                var img = await GetImageLink(link);
                imageUrls.Add(img);
                await Sleep(1000);
            }
        }

        var images = new List<ImageLink>(imageUrls.Count);
        foreach (var (i, link) in imageUrls.Enumerate())
        {
            images.Add(link == "" ? ImageLink.Invalid : new ImageLink(link, FilenameScheme, i));
        }

        return RipInfo.GenerateWithInvalid(images, dirName, FilenameScheme);
    }

    private async Task<string> GetImageLink(string link, CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(link);
        var img = soup.SelectSingleNodeOrThrow("//img[@id='img']").GetSrc();
        return img;
    }
    
    protected override Task<string> UpdateLink(string link, CancellationToken cancellationToken = default)
    {
        return GetImageLink(link);
    }

    private async Task<List<string>> GetImageLinks(HtmlNode soup, string currentUrl)
    {
        List<string> imageLinks;
        if (File.Exists(ImageLinksFileName))
        {
            var imageLinksMap = JsonUtility.Deserialize<Dictionary<string, List<string>>>(ImageLinksFileName);
            // Safety: if TryGetValue fails, imageLinks will be re-initialized, so it won't be null
            if (imageLinksMap is null || !imageLinksMap.TryGetValue(currentUrl, out imageLinks!))
            {
                imageLinks = [];
                await ExtractImageLinks(soup, imageLinks);
            }
        }
        else
        {
            imageLinks = [];
            await ExtractImageLinks(soup, imageLinks);
        }

        return imageLinks;
    }
    
    private async Task ExtractImageLinks(HtmlNode soup, List<string> imageLinks, CancellationToken cancellationToken = default)
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
            var nextPage = soup.SelectSingleNodeOrThrow("//table[@class='ptb']")
                               .SelectNodesSafe(".//a")
                               .Last()
                               .GetHref();
            if (nextPage == CurrentUrl)
            {
                break;
            }
            
            await Sleep(1000);
            try
            {
                pageCount += 1;
                CurrentUrl = nextPage;
            }
            catch (WebDriverTimeoutException)
            {
                Logger.Warning("Timed out. Sleeping for 10 seconds before retrying...");
                await Sleep(10000);
                CurrentUrl = nextPage;
            }
            soup = await Soupify();
        }
    }
}
