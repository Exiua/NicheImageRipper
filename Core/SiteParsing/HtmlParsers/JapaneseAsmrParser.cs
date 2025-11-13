using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class JapaneseAsmrParser : HtmlParser
{
    public JapaneseAsmrParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for japaneseasmr.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='page-title']").InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@class='fotorama__nav__shaft']")
                            .SelectNodesOrThrow(".//img")
                            .Select(img => img.GetSrc())
                            .ToStringImageLinkWrapperList();
        var megaLinks = soup.SelectSingleNodeOrThrow("//div[@class='download_links']")
                            .SelectNodesOrThrow(".//a")
                            .Select(a => a.GetHref());
        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (var link in megaLinks)
        {
            // Unable to bypass Cloudflare atm
            // CurrentUrl = link;
            // while (CurrentUrl == link)
            // {
            //     await Sleep(1000);
            // }
            
            images.Add($"text:{link}");
        }
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
