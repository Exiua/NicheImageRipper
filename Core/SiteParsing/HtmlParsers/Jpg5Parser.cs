using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Utility;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class Jpg5Parser : ParameterizedHtmlParser
{
    public Jpg5Parser(WebDriver driver, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, filenameScheme)
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
            return RipInfo.Empty.WithDirectoryName("Not Found");
        }
        
        string? dirName;
        if (CurrentUrl.Contains("/a/"))
        {
            dirName = soup.SelectNode("//a[@data-text='album-name']").InnerText;
        }
        else if (CurrentUrl.Contains("/img/"))
        {
            dirName = soup.SelectNode("//a[@data-text='image-title']").InnerText;
            single = true;
        }
        else
        {
            dirName = soup.SelectNode("//div[@class='header']").InnerText;
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
    
                var posts = soup.SelectNode("//div[@class='pad-content-listing']")
                                .SelectNodes("./div")
                                .Select(div => div.SelectNode(".//img").GetSrc().Remove(".md"))
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
            var imgSrc = img?.GetSrc();
            if (imgSrc is null || imgSrc.EndsWith("loading.svg"))
            {
                var downloadBtn = soup.SelectNode("//a[@download]");
                var href = downloadBtn.GetHref();
                href = ObfuscationUtility.DeobfuscateJpg5Href(href);
                images.Add(href);
            }
            else
            {
                images.Add(imgSrc);
            }
        }
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
