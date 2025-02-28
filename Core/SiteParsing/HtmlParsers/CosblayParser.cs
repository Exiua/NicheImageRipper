using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using HtmlAgilityPack;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class CosblayParser : HtmlParser
{
    public CosblayParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for cosblay.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250,
        };
        var soup = await Soupify(lazyLoadArgs: lazyLoadArgs, delay: 250);
        var dirName = soup.SelectSingleNode("//h1[@class='entry-title']").InnerText;
        var images = new List<StringImageLinkWrapper>();
        var pageCount = 1;
        while (true)
        {
            Log.Information("Page {PageCount}", pageCount++);
            var imageContainers = soup.SelectSingleNode("//div[@class='entry-content']/p")
                                .SelectNodes(".//img");
            if (imageContainers is null)
            {
                var nextBtn = GetNextButton(soup);
                if (nextBtn is null)
                {
                    break;
                }
                
                throw new RipperException("Images not found");
            }
            
            var imgs = imageContainers.Select(img =>
                                        {
                                            var src = img.GetNullableSrc();
                                            return src ?? img.ParentNode.GetHref();
                                        })
                                .ToStringImageLinks();
            images.AddRange(imgs);
            var nextButton = GetNextButton(soup);
            if (nextButton is null)
            {
                break;
            }
            
            soup = await Soupify(nextButton.GetHref(), lazyLoadArgs: lazyLoadArgs, delay: 250);
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    
        // ReSharper disable once VariableHidesOuterVariable
        HtmlNode? GetNextButton(HtmlNode soup)
        {
            var pager = soup.SelectSingleNode("//div[@class='pgntn-page-pagination-block']");
            var nextButton = pager?.SelectNodes("./a").FirstOrDefault(a => a.InnerText.StartsWith("Next"));
            return nextButton;
        }
    }
}
