using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Utility;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class PixivParser : HtmlParser
{
    public PixivParser(WebDriver driver, Dictionary<string, string> requestHeaders,
                       FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders,
        filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for pixiv.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        const int delay = 500;
        var sessionId = Config.Cookies.Pixiv;
        if (string.IsNullOrEmpty(sessionId))
        {
            Log.Error("Pixiv session ID is not set. Please log in to Pixiv to continue.");
            return RipInfo.Empty;
        }
        
        Driver.SetCookie("PHPSESSID", sessionId);
        if (!CurrentUrl.EndsWith("/artworks"))
        {
            var artworksUrl = CurrentUrl.Split('/').Take(6).Join("/") + "/artworks";
            CurrentUrl = artworksUrl;
        }
        else
        {
            Driver.Refresh();
        }

        const string xpathToFind = "//ul[@class='sc-bf8cea3f-1 bCxfvI']/li";
        var soup = await Soupify(delay: delay, xpath: xpathToFind);
        var dirName = soup.SelectSingleNodeOrThrow("//h1").InnerText;
        var posts = new List<string>();
        var page = 1;
        while (true)
        {
            Log.Information("Parsing page {Page}", page);
            page++;
            var p = soup.SelectSingleNodeOrThrow("//ul[@class='sc-bf8cea3f-1 bCxfvI']")
                           .SelectNodesOrThrow("./li")
                           .Select(li => li.SelectSingleNodeOrThrow(".//a").GetHref())
                           .Select(href => $"https://www.pixiv.net{href}");
            posts.AddRange(p);

            var lastNavButton = soup.SelectSingleNodeOrThrow("//nav[@class='sc-27a0ff07-0 bbkQMy']")
                                    .SelectNodesOrThrow("./a")
                                    .Last();
            var icon = lastNavButton.SelectSingleNode("./svg");
            var disabled = lastNavButton.GetAttributeValue("aria-disabled") == "true";
            if (icon is null || disabled)
            {
                break;
            }

            var nextPageUrl = "https://www.pixiv.net" + lastNavButton.GetHref();
            soup = await Soupify(nextPageUrl, delay: delay, xpath: xpathToFind);
        }
        
        const string xpathToFindImages = "//div[@role='presentation']";
        const string xpathToFindDescription = "//p[starts-with(@id, 'expandable-paragraph-')]";
        //const string xpathToFindDescription = "//div[@class='sc-9f87882a-14 fZWCmd']";
        var images = new List<StringImageLinkWrapper>();
        foreach(var post in posts)
        {
            Log.Information("Parsing post {Post}", post);
            ViewType viewType;
            CurrentUrl = post;
            while (true)
            {
                await Sleep(delay);
                viewType = ViewType.Normal;
                var errorH1 = Driver.TryFindElement(By.XPath("//h1"));
                if (errorH1 is null)
                {
                    break;
                }
                
                var errorText = errorH1.Text;
                if (!errorText.StartsWith("An error"))
                {
                    break;
                }

                Log.Warning("Encountered an error page. Retrying...");
                await Sleep(60000);
                Driver.Refresh();
            }
            
            var buttonClicked = false;
            var showButton = Driver.TryFindElement(By.XPath("//div[@class='sc-9222a8f6-2 eVaEhv']"));
            if (showButton is not null)
            {
                var buttonText = showButton.Text;
                if (buttonText == "Reading works")
                {
                    viewType = ViewType.Webtoon;
                }
                
                showButton.Click();
                buttonClicked = true;
                await Sleep(delay);
            }

            var found = await WaitForElement(xpathToFindDescription);
            if (!found)
            {
                Log.Debug("Description not found for post {Post}", post);
            }
            
            //DebugUtility.Pause();
            soup = await Soupify(xpath: xpathToFindImages);
            var currentCount = images.Count;
            switch (viewType)
            {
                case ViewType.Normal:
                {
                    var imgs = soup.SelectSingleNodeOrThrow("//div[@role='presentation']")
                                   .SelectNodesOrThrow(".//a")
                                   .Select(a => a.GetHref())
                                   .ToStringImageLinks();
                    images.AddRange(imgs);
                    break;
                }
                case ViewType.Webtoon:
                {
                    var imgs = soup.SelectSingleNodeOrThrow("//div[@class='sc-e06c24aa-1 edbfOL']")
                                   .SelectNodesOrThrow(".//a")
                                   .Select(a => a.GetHref())
                                   .ToStringImageLinks();
                    images.AddRange(imgs);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (buttonClicked)
            {
                if (images.Count - currentCount < 2)
                {
                    Log.Warning("Parser may have missed images for {Post}", post);
                }
            }

            var description = soup.SelectSingleNode(xpathToFindDescription);
            if (description is not null)
            {
                Log.Debug("Description found for post {Post}", post);
                var links = description.SelectNodesSafe("./a")
                                       .Select(a => a.GetHref())
                                       .Select(href => href.Remove("/jump.php?"))
                                       .Select(Uri.UnescapeDataString)
                                       .Where(UrlCanBeParsed)
                                       .ToStringImageLinks();
                images.AddRange(links);
            }
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    private enum ViewType
    {
        Normal,
        Webtoon
        
    }
}