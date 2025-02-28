using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class JRantsParser : HtmlParser
{
    public JRantsParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for jrants.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250
        };
        
        var soup = await Soupify(lazyLoadArgs: lazyLoadArgs);
        var dirName = soup.SelectSingleNode("//h1[@class='entry-title']").InnerText;
        var images = new List<StringImageLinkWrapper>();
        var pageCount = 1;
        var noImagesFound = false;
        while (true)
        {
            Log.Information("Parsing page {pageCount}", pageCount);
            pageCount++;
            var imgs = soup.SelectNodes("//div[@class='inside-article']//p/img")?
                            .Select(img => img.GetSrc())
                            .ToStringImageLinks();
            
            if (imgs is not null)
            {
                images.AddRange(imgs);
            }
            else
            {
                noImagesFound = true;
            }
    
            var pagination = soup.SelectSingleNode("//div[@class='pgntn-page-pagination-block']")?
                                    .SelectNodes("./a");
    
            var nextPage = pagination?.FirstOrDefault(a => a.InnerText.StartsWith("Next"));
            if (nextPage is null)
            {
                break;
            }
    
            // Some albums don't have images on the last page, so if we find no images and there is a next page, we throw an exception
            if (noImagesFound)
            {
                throw new RipperException("No images found");
            }
    
            soup = await Soupify(nextPage.GetHref(), lazyLoadArgs: lazyLoadArgs);
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
