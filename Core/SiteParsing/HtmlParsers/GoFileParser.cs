using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using OpenQA.Selenium;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class GoFileParser : ParameterizedHtmlParser, IHtmlParser
{
    public static string ParserName => "gofile";

    public GoFileParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<GoFileParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for gofile.io and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(string url, CancellationToken cancellationToken = default)
    {
        if (url != "")
        {
            CurrentUrl = url;
        }
    
        await SiteLogin(cancellationToken);
        // var cookie = new Cookie("accountToken", cookieValue, ".gofile.io", "/", null, 
        //     true, false, "Lax");
        // Driver.AddCookie(cookie);
        await Sleep(5000, cancellationToken);
        var soup = await Soupify(cancellationToken: cancellationToken);
        var password = soup.SelectSingleNode("//input[@type='password']");
        if (password is not null)
        {
            Logger.Warning("URL is password protected. Writing url to file...");
            await File.WriteAllTextAsync("password_protected_gofile.txt", CurrentUrl + "\n", cancellationToken);
            // TODO: Find a better way to handle password protected files
            return RipInfo.Empty.WithDirectoryName("Password Protected");
        }
        
        var folderNotFound = soup.SelectSingleNode("//div[@class='alert alert-secondary border border-danger text-white']");
        if (folderNotFound is not null)
        {
            Logger.Warning("Folder not found. Writing url to file...");
            // TODO: Find a better way to indicate that data does not exist
            return RipInfo.Empty.WithDirectoryName("Folder Not Found");
        }
        
        var dirName = soup.SelectSingleNodeOrThrow("//span[@id='filesContentFolderName']").InnerText;
        var images = await GoFileParserHelper("", true);
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    
        // ReSharper disable once VariableHidesOuterVariable
        async Task<List<StringFileLinkWrapper>> GoFileParserHelper(string url, bool topLevel = false)
        {
            if (url != "")
            {
                Logger.Debug("Found nested url: {url}", url);
                CurrentUrl = url;
                await Sleep(5000, cancellationToken);
            }
            
            var playButtons = Driver.FindElements(By.CssSelector(
                ".btn.btn-outline-secondary.btn-sm.p-1.me-1.filesContentOption.filesContentOptionPlay.text-white"));
            if(playButtons.Count > 1) // If there is one file, it will be expanded by default
            {
                foreach (var button in playButtons)
                {
                    Driver.ScrollElementIntoView(button);
                    try
                    {
                        button.Click();
                    }
                    catch (ElementNotInteractableException)
                    {
                        Driver.GetScreenshot().SaveAsFile("test.png");
                        Driver.ExecuteScript("arguments[0].click();", button);
                    }
                    await Sleep(125, cancellationToken);
                }
            }
    
            //Driver.WaitUntilElementExists(By.XPath("//div[@id='filesContentTableContent']/div"));
            // ReSharper disable once VariableHidesOuterVariable
            var soup = await Soupify(xpath: "//div[@id='filesContentTableContent']/div[@id]", cancellationToken: cancellationToken);
            var links = new List<StringFileLinkWrapper>();
            var entries = soup.SelectNodesOrThrow("//div[@id='filesContentTableContent']/div");
            foreach (var entry in entries)
            {
                var id = entry.GetNullableAttributeValue("id");
                if (id is null)
                {
                    Logger.Error("Entry has no id: {entry}", entry.InnerHtml);
                    Logger.Error("Current URL: {url}", CurrentUrl);
                    throw new RipperException("Entry has no id");
                }
                
                var anchor = entry.SelectSingleNodeOrThrow(".//a");
                var href = anchor.GetHref();
                if (href.Contains("/d/"))
                {
                    var nestedLinks = await GoFileParserHelper($"https://gofile.io{href}");
                    links.AddRange(nestedLinks);
                }
                else
                {
                    var elm = soup.SelectSingleNode($"//*[@id='elem-{id}']");
                    if (elm is null)
                    {
                        var span = anchor.SelectSingleNodeOrThrow("./span");
                        var filename = span.InnerText;
                        links.Add($"https://cold8.gofile.io/download/web/{id}/{filename}");
                    }
                    else
                    {
                        switch (elm.Name)
                        {
                            case "img":
                            {
                                var src = elm.GetSrc();
                                links.Add(src);
                                break;
                            }
                            case "video":
                            {
                                var source = elm.SelectSingleNodeOrThrow("./source");
                                var src = source.GetSrc();
                                links.Add(src);
                                break;
                            }
                            default:
                            {
                                // TODO: Handle better
                                Logger.Warning("Unknown tag: {tag}", elm.Name);
                                break;
                            }
                        }
                    }
                }
            }
    
            return links;
        }
    }

    protected override async Task<bool> SiteLoginHelper(CancellationToken cancellationToken = default)
    {
        var origUrl = CurrentUrl;
        var loginLink = Config.Custom.GoFile.LoginLink;
        CurrentUrl = loginLink;
        await Sleep(10000, cancellationToken);
        for (var i = 0; i < 4; i++)
        {
            await Sleep(2500, cancellationToken);
            if (CurrentUrl == "https://gofile.io/myProfile")
            {
                Logger.Debug("Logged in to GoFile");
                break;
            }
            
            if (i == 3)
            {
                Logger.Warning("Failed to login to GoFile: {CurrentUrl}", CurrentUrl);
                //Driver.GetScreenshot().SaveAsFile("test2.png");
            }
        }
        
        CurrentUrl = origUrl;
        return true;
    }
}
