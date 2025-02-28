using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class GgoorrParser : HtmlParser
{
    public GgoorrParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for ggoorr.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
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
}
