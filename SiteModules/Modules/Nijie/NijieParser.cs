using System.Text.RegularExpressions;



using OpenQA.Selenium;
using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.Exceptions;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Nijie;
public partial class NijieParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "nijie";
    public static string[] SupportedUrls => ["https://nijie.info/"];

    private const int Delay = 500;
    private const int Retries = 4;
    protected override bool RequiresLogin => true;

    public NijieParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<NijieParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for nijie.info and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var memberId = NijieRegex().Match(CurrentUrl).Groups[1].Value;
        var soup = await Soupify($"https://nijie.info/members_illust.php?id={memberId}", delay: Delay, cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//a[@class='name']").InnerText;
        var posts = new List<string>();
        var count = 1;
        while (true)
        {
            Logger.Information("Parsing illustration posts page {count}", count);
            count++;
            var postTags = soup.SelectSingleNodeOrThrow("//div[@class='mem-index clearboth']").SelectNodesOrThrow(".//p[@class='nijiedao']");
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
                soup = await Soupify($"https://nijie.info{nextPage}", delay: Delay, cancellationToken: cancellationToken);
            }
            else
            {
                break;
            }
        }

        Logger.Information("Parsing illustration posts...");
        var images = new List<StringFileLinkWrapper>();
        foreach (var(i, post)in posts.Enumerate())
        {
            Logger.Information("Parsing illustration post {i}/{posts.Count}", i + 1, posts.Count);
            var postId = post.Split("?")[^1];
            soup = await Soupify($"https://nijie.info/view_popup.php?{postId}", delay: Delay, cancellationToken: cancellationToken);
            IEnumerable<StringFileLinkWrapper> imgs = null!;
            for (var retryCount = 0; retryCount < Retries; retryCount++)
            {
                try
                {
                    var imageWindow = soup.SelectSingleNodeOrThrow("//div[@id='img_window']");
                    var imageNode = imageWindow.SelectNodes(".//a/img");
                    imgs = imageNode is not null ? imageNode.Select(img => (StringFileLinkWrapper)(Protocol + img.GetSrc())) : [(StringFileLinkWrapper)(Protocol + imageWindow.SelectSingleNodeOrThrow(".//video").GetSrc())];
                    break;
                }
                catch (NullReferenceException)
                {
                    await Task.Delay(Delay * 10, cancellationToken);
                    soup = await Soupify($"https://nijie.info/view_popup.php?{postId}", delay: Delay, cancellationToken: cancellationToken);
                    if (retryCount == Retries - 1)
                    {
                        throw new RipperException("Failed to parse illustration post");
                    }
                }
            }

            images.AddRange(imgs);
        }

        soup = await Soupify($"https://nijie.info/members_dojin.php?id={memberId}", delay: Delay, cancellationToken: cancellationToken);
        posts = [];
        var doujins = soup.SelectSingleNodeOrThrow("//div[@class='mem-index clearboth']").SelectNodes("./div");
        if (doujins is null)
        {
            return RipInfo.FromUrlList(images, dirName, FilenameScheme);
        }

        posts.AddRange(doujins.Select(doujin => doujin.SelectSingleNodeOrThrow(".//a").GetHref()));
        foreach (var(i, post)in posts.Enumerate())
        {
            Logger.Information("Parsing doujin post {i}/{posts.Count}", i + 1, posts.Count);
            var postId = post.Split("?")[^1];
            soup = await Soupify($"https://nijie.info/view_popup.php?{postId}", delay: Delay, cancellationToken: cancellationToken);
            IEnumerable<StringFileLinkWrapper> imgs = null!;
            for (var retryCount = 0; retryCount < Retries; retryCount++)
            {
                try
                {
                    imgs = soup.SelectSingleNodeOrThrow("//div[@id='img_window']").SelectNodesOrThrow(".//a/img").Select(img => (StringFileLinkWrapper)(Protocol + img.GetSrc()));
                    break;
                }
                catch (NullReferenceException)
                {
                    await Task.Delay(Delay * 10, cancellationToken);
                    soup = await Soupify($"https://nijie.info/view_popup.php?{postId}", delay: Delay, cancellationToken: cancellationToken);
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

    protected override async Task<bool> SiteLoginHelper(CancellationToken cancellationToken = default)
    {
        var origUrl = CurrentUrl;
        var(username, password) = Config.Logins.GetValueOrDefault(ParserName).Deconstruct();
        if (username.IsNullOrEmpty() || password.IsNullOrEmpty())
        {
            Logger.Warning("No login credentials found for {ParserName}. Please set them in the config file.", ParserName);
            return false;
        }
        
        CurrentUrl = "https://nijie.info/login.php";
        if (CurrentUrl.Contains("age_ver.php"))
        {
            Driver.FindElement(By.XPath("//li[@class='ok']")).Click();
            while (!CurrentUrl.Contains("login.php"))
            {
                await Sleep(100, cancellationToken);
            }
        }

        Driver.FindElement(By.XPath("//input[@name='email']")).SendKeys(username);
        Driver.FindElement(By.XPath("//input[@name='password']")).SendKeys(password);
        Driver.FindElement(By.XPath("//input[@class='login_button']")).Click();
        while (CurrentUrl.Contains("login.php"))
        {
            await Sleep(100, cancellationToken);
        }

        CurrentUrl = origUrl;
        return true;
    }

    [GeneratedRegex(@"id=(\d+)")]
    private static partial Regex NijieRegex();
}