using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class JapaneseAsmrParser : HtmlParser
{
    public JapaneseAsmrParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for japaneseasmr.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='page-title']").InnerText;
        var images = soup.SelectSingleNode("//div[@class='fotorama__nav__shaft']")
                            .SelectNodes(".//img")
                            .Select(img => img.GetSrc())
                            .ToStringImageLinkWrapperList();
        var megaLinks = soup.SelectSingleNode("//div[@class='download_links']")
                            .SelectNodes(".//a")
                            .Select(a => a.GetHref());
        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (var link in megaLinks)
        {
            // Unable to bypass Cloudflare atm
            // CurrentUrl = link;
            // while (CurrentUrl == link)
            // {
            //     await Task.Delay(1000);
            // }
            
            images.Add($"text:{link}");
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
