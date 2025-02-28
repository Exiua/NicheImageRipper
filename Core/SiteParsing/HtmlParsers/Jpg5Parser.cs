using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class Jpg5Parser : ParameterizedHtmlParser
{
    public Jpg5Parser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for jpg5.su and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(string url)
    {
        if(url != "")
        {
            CurrentUrl = url;
        }
    
        var single = false;
        var soup = await Soupify();
        var notFound = soup.SelectSingleNode("//div[@class='page-not-found']");
        if (notFound is not null)
        {
            Log.Warning("Image not found");
            return new RipInfo([], "Not Found", FilenameScheme);
        }
        
        string? dirName;
        if (CurrentUrl.Contains("/a/"))
        {
            dirName = soup.SelectSingleNode("//a[@data-text='album-name']").InnerText;
        }
        else if (CurrentUrl.Contains("/img/"))
        {
            dirName = soup.SelectSingleNode("//a[@data-text='image-title']").InnerText;
            single = true;
        }
        else
        {
            dirName = soup.SelectSingleNode("//div[@class='header']").InnerText;
        }
    
        var images = new List<StringImageLinkWrapper>();
        if (!single)
        {
            var page = 1;
            while (true)
            {
                Log.Information($"Parsing page {page}");
                page++;
                var error = soup.SelectSingleNode("//h1");
                if (error is not null && error.InnerText.StartsWith("500 I"))
                {
                    await Task.Delay(5000); // Most likely due to rate limiting
                    Driver.Refresh();
                    soup = await Soupify();
                }
    
                var posts = soup.SelectSingleNode("//div[@class='pad-content-listing']")
                                .SelectNodes("./div")
                                .Select(div => div.SelectSingleNode(".//img").GetSrc().Remove(".md"))
                                .ToStringImageLinks();
                images.AddRange(posts);
                var nextPage = soup.SelectSingleNode("//a[@data-pagination='next']");
                var nextPageUrl = nextPage?.GetNullableHref();
                if (nextPageUrl is null)
                {
                    break;
                }
    
                nextPageUrl = nextPageUrl.DecodeUrl();
                //Log.Debug("Next page: {nextPageUrl}", nextPageUrl);
                soup = await Soupify(nextPageUrl, xpath: "//div[@class='pad-content-listing']/div");
            }
        }
        else
        {
            var img = soup.SelectSingleNode("//div[@id='image-viewer-container']/img");
            images.Add(img.GetSrc());
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
