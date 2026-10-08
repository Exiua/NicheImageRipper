using NicheImageRipper.Sdk.Common.ExtensionMethods;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.Exceptions;
using NicheImageRipper.Sdk.SiteParsing;
using OpenQA.Selenium;
using PatreonApiClient;
using PatreonApiClient.Models;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Patreon;

public class PatreonParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "patreon";
    public static string[] SupportedUrls { get; } = ["https://www.patreon.com"];

    public PatreonParser(WebDriver driver, Dictionary<string, string> requestHeaders,
                         FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders,
        IHtmlParser.GetFilenameScheme<PatreonParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for patreon.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var sessionIds = Config.Cookies.GetValueOrDefault(ParserName, []);
        if (sessionIds.Length == 0)
        {
            throw new RipperException("No session found for " + ParserName);
        }

        var dirName = CurrentUrl.Split('/')[^1].Split('?')[0];
        var images = new List<StringFileLinkWrapper>();
        var seen = new HashSet<string>();
        using var client = new PatreonClient(sessionIds[0]);

        await foreach (var (i, page) in client.GetPosts(CurrentUrl, cancellationToken)
                                              .EnumerateAsync(cancellationToken: cancellationToken))
        {
            if (i == 0)
            {
                dirName = page.Included.Where(included => included.Type == "campaign")
                              .Select(included => included.Attributes.GetProperty("name").GetString())
                              .FirstOrDefault() ?? dirName;
            }

            foreach (var file in page.GetFiles())
            {
                if (!seen.Add(file.MediaId))
                {
                    continue; // dedupe on media id, not URL
                }

                images.Add(FileLink.WithFilename(file.Url, file.FileName, FilenameScheme));
            }
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}