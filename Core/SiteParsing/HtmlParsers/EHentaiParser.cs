using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class EHentaiParser : HtmlParser
{
    public EHentaiParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for eahentai.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
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
}
