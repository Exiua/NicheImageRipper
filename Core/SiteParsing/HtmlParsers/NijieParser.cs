using System.Text.RegularExpressions;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public partial class NijieParser : HtmlParser
{
    public NijieParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for nijie.info and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        const int delay = 500;
        const int retries = 4;
        SiteName = "nijie";
        await SiteLogin();
        var memberId = NijieRegex().Match(CurrentUrl).Groups[1].Value;
        var soup = await Soupify($"https://nijie.info/members_illust.php?id={memberId}", delay: delay);
        var dirName = soup.SelectSingleNode("//a[@class='name']").InnerText;
        var posts = new List<string>();
        var count = 1;
        while (true)
        {
            Log.Information("Parsing illustration posts page {count}", count);
            count++;
            var postTags = soup.SelectSingleNode("//div[@class='mem-index clearboth']")
                                .SelectNodes(".//p[@class='nijiedao']");
            var postLinks = postTags.Select(link => link.SelectSingleNode(".//a").GetHref());
            posts.AddRange(postLinks);
            var nextPageBtn = soup.SelectSingleNode("//div[@class='right']");
            if (nextPageBtn is null)
            {
                break;
            }
            
            nextPageBtn = nextPageBtn.SelectSingleNode(".//p[@class='page_button']");
            if (nextPageBtn is not null)
            {
                var nextPage = nextPageBtn.SelectSingleNode(".//a").GetHref().Replace("&amp;", "&");
                soup = await Soupify($"https://nijie.info{nextPage}", delay: delay);
            }
            else
            {
                break;
            }
        }
        
        Log.Information("Parsing illustration posts...");
        var images = new List<StringImageLinkWrapper>();
        foreach (var (i, post) in posts.Enumerate())
        {
            Log.Information("Parsing illustration post {i}/{posts.Count}", i + 1, posts.Count);
            var postId = post.Split("?")[^1];
            soup = await Soupify($"https://nijie.info/view_popup.php?{postId}", delay: delay);
            IEnumerable<StringImageLinkWrapper> imgs = null!;
            for(var retryCount = 0; retryCount < retries; retryCount++)
            {
                try
                {
                    var imageWindow = soup.SelectSingleNode("//div[@id='img_window']");
                    var imageNode = imageWindow.SelectNodes(".//a/img");
                    imgs = imageNode is not null 
                        ? imageNode.Select(img => (StringImageLinkWrapper)(Protocol + img.GetSrc())) 
                        : [(StringImageLinkWrapper)(Protocol + imageWindow.SelectSingleNode(".//video").GetSrc())];
                    break;
                }
                catch (NullReferenceException)
                {
                    await Task.Delay(delay * 10);
                    soup = await Soupify($"https://nijie.info/view_popup.php?{postId}", delay: delay);
                    if (retryCount == retries - 1)
                    {
                        throw new RipperException("Failed to parse illustration post");
                    }
                }
            }
            
            images.AddRange(imgs);
        }
        
        soup = await Soupify($"https://nijie.info/members_dojin.php?id={memberId}", delay: delay);
        posts = [];
        var doujins = soup.SelectSingleNode("//div[@class='mem-index clearboth']")
                            .SelectNodes("./div");
        if (doujins is null)
        {
            return new RipInfo(images, dirName, FilenameScheme);
        }
        
        posts.AddRange(doujins.Select(doujin => doujin.SelectSingleNode(".//a").GetHref()));
        foreach (var (i, post) in posts.Enumerate())
        {
            Log.Information("Parsing doujin post {i}/{posts.Count}", i + 1, posts.Count);
            var postId = post.Split("?")[^1];
            soup = await Soupify($"https://nijie.info/view_popup.php?{postId}", delay: delay);
            IEnumerable<StringImageLinkWrapper> imgs = null!;
            for(var retryCount = 0; retryCount < retries; retryCount++)
            {
                try
                {
                    imgs = soup.SelectSingleNode("//div[@id='img_window']")
                                .SelectNodes(".//a/img")
                                .Select(img => (StringImageLinkWrapper)(Protocol + img.GetSrc()));
                    break;
                }
                catch (NullReferenceException)
                {
                    await Task.Delay(delay * 10);
                    soup = await Soupify($"https://nijie.info/view_popup.php?{postId}", delay: delay);
                    if (retryCount == retries - 1)
                    {
                        throw new RipperException("Failed to parse doujin post");
                    }
                }
            }
            images.AddRange(imgs);
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }

    protected override async Task<bool> SiteLoginHelper()
    {
        var origUrl = CurrentUrl;
        var (username, password) = Config.Logins["Nijie"];
        CurrentUrl = "https://nijie.info/login.php";
        if (CurrentUrl.Contains("age_ver.php"))
        {
            Driver.FindElement(By.XPath("//li[@class='ok']")).Click();
            while (!CurrentUrl.Contains("login.php"))
            {
                await Task.Delay(100);
            }
        }
        
        Driver.FindElement(By.XPath("//input[@name='email']")).SendKeys(username);
        Driver.FindElement(By.XPath("//input[@name='password']")).SendKeys(password);
        Driver.FindElement(By.XPath("//input[@class='login_button']")).Click();
        while (CurrentUrl.Contains("login.php"))
        {
            await Task.Delay(100);
        }
        
        CurrentUrl = origUrl;
        return true;
    }
    
    [GeneratedRegex(@"id=(\d+)")]
    private static partial Regex NijieRegex();
}
