using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Utility;
using Serilog;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class LadyLapParser : HtmlParser
{
    public LadyLapParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for ladylap.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        const string domain = "https://www.ladylap.com";
        const int delay = 1000;
    
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//strong").InnerText;
        var numPages = soup.SelectSingleNode("//ul[@class='pagination pagination']")
                            .SelectNodes("./li")
                            .Count;
        var baseUrl = CurrentUrl;
        var images = new List<StringImageLinkWrapper>();
        for (var i = 0; i < numPages; i++)
        {
            Log.Information("Parsing page {i} of {numPages}", i + 1, numPages);
            var posts = soup.SelectNodes("//div[@class='col-md-12 col-lg-12']")[2]
                            .SelectNodes(".//a")
                            .Select(a => domain + a.GetHref())
                            .ToStringImageLinks();
            images.AddRange(posts);
            soup = await Soupify($"{baseUrl}/{i + 2}"); // Pages are 1-indexed
            await Task.Delay(delay);
        }
    
        var cookieJar = Driver.GetCookieJar();
        var cookies = cookieJar.AllCookies
                                .Aggregate("", (current, cookie) => current + $"{cookie.Name}={cookie.Value}; ")
                                .Trim();
        RequestHeaders[RequestHeaderKeys.Cookie] = cookies;
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
