using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class TitsInTopsParser : HtmlParser
{
    public TitsInTopsParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for titsintops.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        const string siteUrl = "https://titsintops.com";
        await SiteLogin();
        var cookies = Driver.GetCookieJar();
        var cookieStr = cookies.AllCookies.Aggregate("", (current, cookie) => current + $"{cookie.Name}={cookie.Value};");
        RequestHeaders["cookie"] = cookieStr;
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='p-title-value']")
                            .InnerText;
        var images = new List<StringImageLinkWrapper>();
        var externalLinks = CreateExternalLinkDict();
        var pageCount = 1;
        while (true)
        {
            Log.Information("Parsing page {PageCount}", pageCount);
            pageCount++;
            var posts = soup.SelectSingleNode("//div[@class='block-body js-replyNewMessageContainer']")
                            .SelectNodes(".//div[@class='message-content js-messageContent']");
            foreach (var post in posts)
            {
                var imgs = post.SelectSingleNode(".//article[@class='message-body js-selectToQuote']")
                                .SelectNodes(".//img");
                if (imgs is not null)
                {
                    var imgList = imgs.Select(im => im.GetSrc())
                                        .Where(im => im.Contains("http"));
                    images.AddRange(imgList.Select(im => (StringImageLinkWrapper)im));
                }
                
                var videos = post.SelectNodes(".//video");
                if (videos is not null)
                {
                    var videoUrls = videos.Select(vid => $"https://titsintops.com{vid.SelectSingleNode(".//source").GetSrc()}");
                    images.AddRange(videoUrls.Select(vid => (StringImageLinkWrapper)vid));
                }
                
                var iframes = post.SelectNodes(".//iframe");
                if (iframes is not null)
                {
                    var embeddedUrls = iframes.Select(em => em.GetSrc())
                                                .Where(em => em.Contains("http"));
                    embeddedUrls = await ParseEmbeddedUrls(embeddedUrls);
                    images.AddRange(embeddedUrls.Select(em => (StringImageLinkWrapper)em));
                }
                
                var attachments = post.SelectSingleNode(".//ul[@class='attachmentList']");
                var attachments2 = attachments?.SelectNodes(".//a[@class='file-preview js-lbImage']");
                if (attachments2 != null)
                {
                    var attachList = attachments2.Select(attach => $"https://titsintops.com{attach.GetHref()}");
                    images.AddRange(attachList.Select(attach => (StringImageLinkWrapper)attach));
                }

                var links = post.SelectSingleNode(".//article[@class='message-body js-selectToQuote']")
                                .SelectNodes(".//a");
                if (links is not null)
                {
                    var linkList = links.Select(link => link.GetNullableHref())
                                        .Where(link => link is not null);
                    var filteredLinks = ExtractExternalUrls(linkList!);
                    var downloadableLinks = await ExtractDownloadableLinks(filteredLinks, externalLinks);
                    images.AddRange(downloadableLinks.Select(link => (StringImageLinkWrapper)link));
                }
            }
    
            var nextPage = soup.SelectSingleNode("//a[@class='pageNav-jump pageNav-jump--next']");
            if (nextPage is null)
            {
                SaveExternalLinks(externalLinks);
                break;
            }
    
            var nextPageUrl = nextPage.GetHref();
            soup = await Soupify($"{siteUrl}{nextPageUrl}");
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }

    protected override async Task<bool> SiteLoginHelper()
    {
        var origUrl = CurrentUrl;
        var (username, password) = Config.Logins["TitsInTops"];
        CurrentUrl = "https://titsintops.com/phpBB2/index.php?login/login";
        var loginInput = Driver.TryFindElement(By.XPath("//input[@name='login']"));
        while (loginInput is null)
        {
            await Task.Delay(100);
            loginInput = Driver.TryFindElement(By.XPath("//input[@name='login']"));
        }
        loginInput.SendKeys(username);
        var passwordInput = Driver.FindElement(By.XPath("//input[@name='password']"));
        passwordInput.SendKeys(password);
        Driver.FindElement(By.XPath("//button[@class='button--primary button button--icon button--icon--login']")).Click();
        while (Driver.TryFindElement(By.XPath("//button[@class='button--primary button button--icon button--icon--login']")) is not null)
        {
            await Task.Delay(100);
        }
        
        CurrentUrl = origUrl;
        return true;
    }
}
