using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.Utility;
using HtmlAgilityPack;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class EHentaiParser : HtmlParser
{
    private const string ImageLinksFileName = "ehentai.json";

    // Quick fix for updating links, should be replaced with a more robust solution
    private static string _lastUrl = "";
    
    public EHentaiParser(WebDriver driver, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for e-hentai.org and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var currentUrl = CurrentUrl;
        _lastUrl = currentUrl;
        var soup = await Soupify();
        var dirName = soup.SelectNode("//h1[@id='gn']").InnerText;
        var imageLinks = await GetImageLinks(soup, currentUrl);
        
        Log.Debug("Found {count} image links", imageLinks.Count);
        
        var imageLinksSave = new Dictionary<string, List<string>>
        {
            [currentUrl] = imageLinks
        };
        
        JsonUtility.Serialize(ImageLinksFileName, imageLinksSave);
        
        var images = new List<StringImageLinkWrapper>();
        foreach (var (i, link) in imageLinks.Enumerate())
        {
            // TODO: Handle links missing keystamp or fileindex
            // TODO: Handle files that download as invalid request
            Log.Information("Parsing image {i} of {count}", i + 1, imageLinks.Count);
            var img = await GetImageLink(link);
            images.Add(img);
            // if (i != 0 && i % 150 == 0)
            // {
            //     await Task.Delay(5000);
            // }
            // else
            // {
            //     await Task.Delay(1000);
            // }
            await Task.Delay(1000);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    private async Task<string> GetImageLink(string link)
    {
        var soup = await Soupify(link);
        var img = soup.SelectNode("//img[@id='img']").GetSrc();
        return img;
    }

    // Should never be called before Parse() is called
    public async Task<List<ImageLink>> UpdateLinks(List<ImageLink> links, int start)
    {
        if (!File.Exists(ImageLinksFileName))
        {
            throw new RipperException("Image links file does not exist. Please run Parse() first.");
        }
        
        var imageLinksMap = JsonUtility.Deserialize<Dictionary<string, List<string>>>(ImageLinksFileName);
        if (imageLinksMap is null || !imageLinksMap.TryGetValue(_lastUrl, out var imageLinks))
        {
            throw new RipperException("Image links not found in the file. Please run Parse() first.");
        }
        
        if (start < 0 || start >= imageLinks.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "Start index is out of range.");
        }
        
        Log.Information("Updating links from link {start} of {count}", start + 1, imageLinks.Count);
        foreach (var (i, link) in imageLinks.Enumerate())
        {
            if (i < start)
            {
                continue;
            }
            
            var img = await GetImageLink(link);
            Log.Information("Updating image link {i} of {count}", i + 1, imageLinks.Count);
            links[i].Url = img;
        }
        
        Log.Information("Updated {count} image links", imageLinks.Count - start);
        return links;
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
    
    private async Task ExtractImageLinks(HtmlNode soup, List<string> imageLinks)
    {
        var pageCount = 1;
        while (true)
        {
            Log.Information("Parsing page {pageCount}", pageCount);
            var imageTags = soup.SelectNodesSafe("//div[@id='gdt']/a").GetHrefs();
            if (imageTags.Count == 0)
            {
                Log.Warning("No image links found on page {pageCount}. Stopping parsing.", pageCount);
                break;
            }
            
            imageLinks.AddRange(imageTags);
            var nextPage = soup.SelectNode("//table[@class='ptb']")
                               .SelectNodesSafe(".//a")
                               .Last()
                               .GetHref();
            if (nextPage == CurrentUrl)
            {
                break;
            }
            
            await Task.Delay(1000);
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
    }
}
