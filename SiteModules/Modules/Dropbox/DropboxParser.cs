using HtmlAgilityPack;


using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.Exceptions;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Dropbox;

public class DropboxParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "dropbox";
    public static string[] SupportedUrls => ["https://www.dropbox.com/"];

    public DropboxParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                         FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<DropboxParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for dropbox.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns></returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        return await Parse("", cancellationToken);
    }

    /// <summary>
    ///     Parses  the HTML for dropbox.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <param name = "dropboxUrl"></param>
    /// <returns></returns>
    internal async Task<RipInfo> Parse(string dropboxUrl, CancellationToken cancellationToken = default)
    {
        var internalUse = false;
        if (!string.IsNullOrEmpty(dropboxUrl))
        {
            if (dropboxUrl.Contains("/scl/fi/"))
            {
                dropboxUrl = dropboxUrl.Replace("dl=0", "dl=1");
                return RipInfo.FromUrlList([dropboxUrl], "", FilenameScheme);
            }

            CurrentUrl = dropboxUrl;
            internalUse = true;
        }

        var soup = await Soupify(xpath: "//span[@class='dig-Breadcrumb-link-text']",
            cancellationToken: cancellationToken);
        string dirName;
        if (!internalUse)
        {
            try
            {
                dirName = soup.SelectSingleNodeOrThrow("//span[@class='dig-Breadcrumb-link-text']").InnerText;
            }
            catch (NullReferenceException)
            {
                var deletedNotice =
                    soup.SelectSingleNode("//h2[@class='dig-Title dig-Title--size-large dig-Title--color-standard']");
                if (deletedNotice is not null)
                {
                    return RipInfo.FromUrlList([], "Deleted", FilenameScheme);
                }

                Logger.Error("Could not find directory name. Unable to continue.");
                throw;
            }
        }
        else
        {
            dirName = "";
        }

        if (CurrentUrl.Contains("/scl/fi/"))
        {
            return RipInfo.FromUrlList([CurrentUrl.Replace("dl=0", "dl=1")], "", FilenameScheme);
        }

        var images = new List<string>();
        var filenames = new List<string>();
        var postsNodes = soup.SelectSingleNode("//ol[@class='_sl-grid-body_6yqpe_26']");
        var posts = new List<string>();
        if (postsNodes is not null)
        {
            posts = postsNodes.SelectNodesOrThrow("//a").GetHrefs().RemoveDuplicates();
            foreach (var post in posts)
            {
                soup = await Soupify(post, xpath: "//img[@class='_fullSizeImg_1anuf_16']",
                    cancellationToken: cancellationToken);
                GetDropboxFile(soup, post, filenames, images,
                    posts); // The method modifies the posts list which is being iterated over...
            }
        }
        else
        {
            GetDropboxFile(soup, CurrentUrl, filenames, images, posts);
        }

        return RipInfo.FromUrlListWithFilenames(images.ToStringFileLinkWrapperList(), dirName, FilenameScheme,
            filenames);
    }

    private static void GetDropboxFile(HtmlNode soup, string post, List<string> filenames, List<string> images,
                                       List<string> posts)
    {
        var filename = post.Split("/")[^1].Split("?")[0];
        filenames.Add(filename);
        var img = soup.SelectSingleNode("//img[@class='_fullSizeImg_1anuf_16']");
        try
        {
            if (img is not null)
            {
                var src = img.GetSrc();
                if (src != "")
                {
                    images.Add(src);
                }
            }
            else
            {
                var vid = soup.SelectSingleNode("//video");
                if (vid is not null)
                {
                    var src = vid.SelectSingleNodeOrThrow("//source").GetSrc();
                    if (src != "")
                    {
                        images.Add(src);
                    }
                }
                else
                {
                    var newPosts = soup.SelectSingleNodeOrThrow("//ol[@class='_sl-grid-body_6yqpe_26']")
                                       .SelectNodesOrThrow("//a").GetHrefs().RemoveDuplicates();
                    posts.AddRange(newPosts);
                }
            }
        }
        catch (AttributeNotFoundException)
        {
            // ignored
        }
    }
}