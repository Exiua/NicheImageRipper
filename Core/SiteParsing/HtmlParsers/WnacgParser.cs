using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class WnacgParser : HtmlParser
{
    public WnacgParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for wnacg.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
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
}
