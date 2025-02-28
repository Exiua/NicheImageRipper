using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using HtmlAgilityPack;
using Serilog;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class DropboxParser : HtmlParser
{
    public DropboxParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for dropbox.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <param name="dropboxUrl"></param>
    /// <returns></returns>
    public override async Task<RipInfo> Parse()
    {
        return await Parse("");
    }

    /// <summary>
    ///     Parses the html for dropbox.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <param name="dropboxUrl"></param>
    /// <returns></returns>
    internal async Task<RipInfo> Parse(string dropboxUrl)
    {
        var internalUse = false;
        if (!string.IsNullOrEmpty(dropboxUrl))
        {
            if (dropboxUrl.Contains("/scl/fi/"))
            {
                dropboxUrl = dropboxUrl.Replace("dl=0", "dl=1");
                return new RipInfo([dropboxUrl], "", FilenameScheme);
            }
    
            CurrentUrl = dropboxUrl;
            internalUse = true;
        }
    
        var soup = await Soupify(xpath: "//span[@class='dig-Breadcrumb-link-text']");
        string dirName;
        if (!internalUse)
        {
            try
            {
                dirName = soup.SelectSingleNode("//span[@class='dig-Breadcrumb-link-text']").InnerText;
            }
            catch (NullReferenceException)
            {
                var deletedNotice =
                    soup.SelectSingleNode("//h2[@class='dig-Title dig-Title--size-large dig-Title--color-standard']");
                if (deletedNotice is not null)
                {
                    return new RipInfo([], "Deleted", FilenameScheme);
                }
    
                Log.Error("Could not find directory name. Unable to continue.");
                throw;
            }
        }
        else
        {
            dirName = "";
        }
    
        if (CurrentUrl.Contains("/scl/fi/"))
        {
            return new RipInfo([CurrentUrl.Replace("dl=0", "dl=1")], "", FilenameScheme);
        }
    
        var images = new List<string>();
        var filenames = new List<string>();
        var postsNodes = soup.SelectSingleNode("//ol[@class='_sl-grid-body_6yqpe_26']");
        var posts = new List<string>();
        if (postsNodes is not null)
        {
            posts = postsNodes.SelectNodes("//a").GetHrefs().RemoveDuplicates();
            foreach (var post in posts)
            {
                soup = await Soupify(post, xpath: "//img[@class='_fullSizeImg_1anuf_16']");
                GetDropboxFile(soup, post, filenames, images,
                    posts); // The method modifies the posts list which is being iterated over...
            }
        }
        else
        {
            GetDropboxFile(soup, CurrentUrl, filenames, images, posts);
        }
    
        return new RipInfo(images.ToStringImageLinkWrapperList(), dirName, FilenameScheme, filenames: filenames);
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
                    var src = vid.SelectSingleNode("//source").GetSrc();
                    if (src != "")
                    {
                        images.Add(src);
                    }
                }
                else
                {
                    var newPosts = soup
                                  .SelectSingleNode("//ol[@class='_sl-grid-body_6yqpe_26']")
                                  .SelectNodes("//a")
                                  .GetHrefs()
                                  .RemoveDuplicates();
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
