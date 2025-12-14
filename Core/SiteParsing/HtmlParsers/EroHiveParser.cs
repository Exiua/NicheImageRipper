using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class EroHiveParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "erohive";

    public EroHiveParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for erohive.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        var baseUrl = CurrentUrl.Split("?")[0];
        await WaitForPostLoad();
        var soup = await Soupify();
        var dirName = soup
                        .SelectSingleNodeOrThrow("//h1[@class='title']")
                        .InnerText
                        .Split(" ")
                        .SkipLast(3)
                        .Join(" ");
        var posts = new List<string>();
        var page = 0;
        while (true)
        {
            Log.Information("Parsing page {page}", page);
            if (page != 0)
            {
                CurrentUrl = $"{baseUrl}?p={page}";
                await WaitForPostLoad();
                soup = await Soupify();
            }
    
            page += 1;
            var links = soup.SelectNodesOrThrow("//a[@class='image-thumb']")
                            .Select(link => link.GetHref()).ToList();
            if (links.Count == 0)
            {
                break;
            }
    
            posts.AddRange(links);
        }
        
        var images = new List<StringImageLinkWrapper>();
        var total = posts.Count;
        foreach (var (i, post) in posts.Enumerate())
        {
            Log.Information("Parsing post {i} of {total}", i + 1, total);
            CurrentUrl = post;
            await WaitForPostLoad();
            soup = await Soupify();
            var img = soup.SelectSingleNodeOrThrow("//div[@class='img']")
                            .SelectSingleNodeOrThrow(".//img")
                            .GetSrc();
            images.Add(img);
        }
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    
        async Task WaitForPostLoad()
        {
            while (true)
            {
                var elm = Driver.TryFindElement(By.Id("has_no_img"));
                if (elm is not null && elm.GetDomAttribute("class") != "")
                {
                    await Sleep(100);
                    break;
                }
    
                if(Driver.FindElements(By.XPath("//h2[@class='warning-page']")).Count > 0)
                {
                    await Sleep(5000);
                    await Driver.Navigate().RefreshAsync();
                }
                
                await Sleep(100);
            }
        }
    }
}
