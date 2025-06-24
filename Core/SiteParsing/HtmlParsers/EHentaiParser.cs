using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class EHentaiParser : HtmlParser
{
    public EHentaiParser(WebDriver driver, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for e-hentai.org and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectNode("//h1[@id='gn']").InnerText;
        var imageLinks = new List<string>();
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
        
        Log.Debug("Found {count} image links", imageLinks.Count);
        
        var images = new List<StringImageLinkWrapper>();
        foreach (var (i, link) in imageLinks.Enumerate())
        {
            Log.Information("Parsing image {i} of {count}", i + 1, imageLinks.Count);
            await Task.Delay(2500);
            soup = await Soupify(link);
            var img = soup.SelectNode("//img[@id='img']").GetSrc();
            images.Add(img);
        }
        
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
