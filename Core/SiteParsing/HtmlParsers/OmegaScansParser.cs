using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class OmegaScansParser : HtmlParser
{
    public OmegaScansParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for omegascans.org and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns></returns>
    public override async Task<RipInfo> Parse()
    {
        await Sleep(5000);
        var soup = await Soupify();
        var dirName = soup
                        .SelectSingleNodeOrThrow(
                            "//h1[@class='text-xl md:text-3xl text-primary font-bold text-center lg:text-left']")
                        .InnerText;
        var chapterCountStr = soup.SelectSingleNodeOrThrow("//div[@class='space-y-2 rounded p-5 bg-foreground']")
                                    .SelectNodesOrThrow("./div[@class='flex justify-between']")[3]
                                    .SelectSingleNodeOrThrow(".//span[@class='text-secondary line-clamp-1']").InnerText;
        var chapterCount = int.Parse(chapterCountStr.Trim().Split(' ')[0]);
        List<string> chapters = [];
        while (true)
        {
            var links = soup.SelectSingleNodeOrThrow("//ul[@class='grid grid-cols-1 gap-y-8']")
                            .SelectNodesOrThrow("./a[@href]").GetHrefs().Select(link => $"https://omegascans.org{link}")
                            .ToList();
            chapters.AddRange(links);
            if (chapters.Count == chapterCount)
            {
                break;
            }
    
            Driver.FindElement(By.XPath("//nav[@class='mx-auto flex w-full justify-center gap-x-2']/ul[last()]//a"))
                    .Click();
            soup = await Soupify();
        }
    
        chapters.Reverse();
        var images = new List<StringImageLinkWrapper>();
        foreach (var (i, chapter) in chapters.Enumerate())
        {
            Log.Information("Parsing chapter {i} of {chapterCount}", i + 1, chapterCount);
            CurrentUrl = chapter;
            await LazyLoad(scrollBy: true, increment: 5000);
            soup = await Soupify();
            var post = soup.SelectSingleNode("//p[@class='flex flex-col justify-center items-center']");
            if (post is null)
            {
                Log.Warning("Post not found");
                continue;
            }
    
            var imgs = post.SelectNodesOrThrow("./img[@src]").GetSrcs();
            images.AddRange(imgs.Select(img => (StringImageLinkWrapper)img));
        }
    
        // Files are numbered per chapter, so original will have the files overwrite each other
        return RipInfo.FromUrlList(images, dirName, FilenameScheme, nameReuse: true);
    }
}
