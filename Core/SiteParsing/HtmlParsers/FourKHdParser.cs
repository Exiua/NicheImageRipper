using Common.ExtensionMethods;
using System.Text.RegularExpressions;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public partial class FourKHdParser : HtmlParser
{
    public FourKHdParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for 4khd.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        await LazyLoad(new LazyLoadArgs
        {
            StopElement = By.XPath("//ul[@class='page-links']")
        });
        
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h3").InnerText;
        var numPages = soup.SelectSingleNode("//ul[@class='page-links']")?
                            .SelectNodes("./li")?
                            .Count ?? 1;
    
        var baseUrl = CurrentUrl;
        var images = new List<StringImageLinkWrapper>();
        for (var page = 1; page <= numPages; page++)
        {
            Log.Information("Parsing page {page} of {numPages}", page, numPages);
            var baseElement = soup.SelectSingleNode("//div[@id='basicExample']") ?? soup.SelectSingleNodeOrThrow("//div[@id='basicE']");
            var imgs = baseElement.SelectNodesOrThrow("./a")
                                    .Select(a => a.GetHref().Split("?")[0])
                                    .ToStringImageLinks();
            images.AddRange(imgs);
            // The first page is already loaded
            soup = await Soupify($"{baseUrl}/{page + 1}", lazyLoadArgs: new LazyLoadArgs
            {
                StopElement = By.XPath("//ul[@class='page-links']")
            });
        }
    
        var baseName = images[0].Split("/")[^1];
        var match = FourKHdRegex().Match(baseName);
        baseName = match.Groups[1].Value;
        images = images.Where(img => img.Contains(baseName)).ToList();
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    [GeneratedRegex(@"([a-zA-Z0-9-]+)")]
    private static partial Regex FourKHdRegex();
}
