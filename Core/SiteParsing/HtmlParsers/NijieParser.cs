using Common.ExtensionMethods;
using System.Text.RegularExpressions;
using Core.Configuration;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.Managers;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public partial class NijieParser : HtmlParser
{
    private const int Delay = 500;
    private const int Retries = 4;
    
    public NijieParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<NijieParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for nijie.info and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        await SiteLogin();
        var memberId = NijieRegex().Match(CurrentUrl).Groups[1].Value;
        var soup = await Soupify($"https://nijie.info/members_illust.php?id={memberId}", delay: Delay);
        var dirName = soup.SelectSingleNodeOrThrow("//a[@class='name']").InnerText;
        var posts = new List<string>();
        var count = 1;
        while (true)
        {
            Log.Information("Parsing illustration posts page {count}", count);
            count++;
            var postTags = soup.SelectSingleNodeOrThrow("//div[@class='mem-index clearboth']")
                                .SelectNodesOrThrow(".//p[@class='nijiedao']");
            var postLinks = postTags.Select(link => link.SelectSingleNodeOrThrow(".//a").GetHref());
            posts.AddRange(postLinks);
            var nextPageBtn = soup.SelectSingleNode("//div[@class='right']");
            if (nextPageBtn is null)
            {
                break;
            }
            
            nextPageBtn = nextPageBtn.SelectSingleNode(".//p[@class='page_button']");
            if (nextPageBtn is not null)
            {
                var nextPage = nextPageBtn.SelectSingleNodeOrThrow(".//a").GetHref().Replace("&amp;", "&");
                soup = await Soupify($"https://nijie.info{nextPage}", delay: Delay);
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
            soup = await Soupify($"https://nijie.info/view_popup.php?{postId}", delay: Delay);
            IEnumerable<StringImageLinkWrapper> imgs = null!;
            for(var retryCount = 0; retryCount < Retries; retryCount++)
            {
                try
                {
                    var imageWindow = soup.SelectSingleNodeOrThrow("//div[@id='img_window']");
                    var imageNode = imageWindow.SelectNodes(".//a/img");
                    imgs = imageNode is not null 
                        ? imageNode.Select(img => (StringImageLinkWrapper)(Protocol + img.GetSrc())) 
                        : [(StringImageLinkWrapper)(Protocol + imageWindow.SelectSingleNodeOrThrow(".//video").GetSrc())];
                    break;
                }
                catch (NullReferenceException)
                {
                    await Task.Delay(Delay * 10);
                    soup = await Soupify($"https://nijie.info/view_popup.php?{postId}", delay: Delay);
                    if (retryCount == Retries - 1)
                    {
                        throw new RipperException("Failed to parse illustration post");
                    }
                }
            }
            
            images.AddRange(imgs);
        }
        
        soup = await Soupify($"https://nijie.info/members_dojin.php?id={memberId}", delay: Delay);
        posts = [];
        var doujins = soup.SelectSingleNodeOrThrow("//div[@class='mem-index clearboth']")
                            .SelectNodes("./div");
        if (doujins is null)
        {
            return RipInfo.FromUrlList(images, dirName, FilenameScheme);
        }
        
        posts.AddRange(doujins.Select(doujin => doujin.SelectSingleNodeOrThrow(".//a").GetHref()));
        foreach (var (i, post) in posts.Enumerate())
        {
            Log.Information("Parsing doujin post {i}/{posts.Count}", i + 1, posts.Count);
            var postId = post.Split("?")[^1];
            soup = await Soupify($"https://nijie.info/view_popup.php?{postId}", delay: Delay);
            IEnumerable<StringImageLinkWrapper> imgs = null!;
            for(var retryCount = 0; retryCount < Retries; retryCount++)
            {
                try
                {
                    imgs = soup.SelectSingleNodeOrThrow("//div[@id='img_window']")
                                .SelectNodesOrThrow(".//a/img")
                                .Select(img => (StringImageLinkWrapper)(Protocol + img.GetSrc()));
                    break;
                }
                catch (NullReferenceException)
                {
                    await Task.Delay(Delay * 10);
                    soup = await Soupify($"https://nijie.info/view_popup.php?{postId}", delay: Delay);
                    if (retryCount == Retries - 1)
                    {
                        throw new RipperException("Failed to parse doujin post");
                    }
                }
            }
            images.AddRange(imgs);
        }
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    protected override async Task<bool> SiteLoginHelper()
    {
        var origUrl = CurrentUrl;
        var (username, password) = Config.Logins.Nijie;
        CurrentUrl = "https://nijie.info/login.php";
        if (CurrentUrl.Contains("age_ver.php"))
        {
            Driver.FindElement(By.XPath("//li[@class='ok']")).Click();
            while (!CurrentUrl.Contains("login.php"))
            {
                await Sleep(100);
            }
        }
        
        Driver.FindElement(By.XPath("//input[@name='email']")).SendKeys(username);
        Driver.FindElement(By.XPath("//input[@name='password']")).SendKeys(password);
        Driver.FindElement(By.XPath("//input[@class='login_button']")).Click();
        while (CurrentUrl.Contains("login.php"))
        {
            await Sleep(100);
        }
        
        CurrentUrl = origUrl;
        return true;
    }
    
    [GeneratedRegex(@"id=(\d+)")]
    private static partial Regex NijieRegex();
}
