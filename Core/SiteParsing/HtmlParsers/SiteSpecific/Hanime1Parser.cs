using CSWebDriverClient;
using HtmlAgilityPack;
using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using ErrorResponse = CSWebDriverClient.Models.Responses.ErrorResponse;
using NotSupportedException = NicheImageRipper.Core.Exceptions.NotSupportedException;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

public class Hanime1Parser : HtmlParser, IHtmlParser
{
    public static string ParserName => "hanime1";
    public static string[] SupportedUrls => ["https://hanime1.me/"];

    public Hanime1Parser(WebDriver driver, ApiClientManager apiClientManager, Dictionary<string, string> requestHeaders,
                         FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, apiClientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<Hanime1Parser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for hanime1.me and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        string dirName;
        List<StringFileLinkWrapper> images = [];
        var client = new Client(Config.CSWebDriverUri);
        var response = await client.GetPage(CurrentUrl);
        var soup = await Soupify(response);
        if (CurrentUrl.Contains("/watch?"))
        {
            dirName = soup.SelectSingleNodeOrThrow("//h3").InnerText;
            var url = await GetVideoUrl(client, CurrentUrl);
            images.Add(url);
        }
        else if (CurrentUrl.Contains("/search?"))
        {
            dirName = CurrentUrl.Split("query=")[1].Split('&')[0].UnescapeUrl();
            List<string> entries = [];
            var numPages = soup.SelectSingleNodeOrThrow("//div[@class='skip-page-wrapper']/div").InnerText.Split(';')[2]
                               .Trim().ParseInt();
            var baseUrl = CurrentUrl.Split("&page=")[0];
            for (var i = 0; i < numPages; i++)
            {
                Logger.Information("Parsing page {Page}", i + 1);
                var posts = soup.SelectSingleNodeOrThrow("//div[@class='row no-gutter']").SelectNodesOrThrow("./div")
                                .Select(div => div.SelectSingleNodeOrThrow("./a").GetHref());
                entries.AddRange(posts);
                if (i == numPages - 1)
                {
                    break;
                }

                response = await client.GetPage($"{baseUrl}&page={i + 2}",
                    waitForXPath: "//div[@class='row no-gutter']//a");
                soup = await Soupify(response);
            }

            entries = entries.Distinct().ToList();
            foreach (var (i, entry)in entries.Enumerate())
            {
                Logger.Information("Parsing entry {Entry} ({Index}/{Total})", entry, i + 1, entries.Count);
                var url = await GetVideoUrl(client, entry);
                images.Add(url);
                await Sleep(250);
            }
        }
        else
        {
            throw new NotSupportedException("Hanime1 Parser", $"This page type is not supported: {CurrentUrl}");
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    private async Task<string> GetVideoUrl(Client client, string url, CancellationToken cancellationToken = default)
    {
        url = url.Replace("watch?", "download?");
        var response = await client.GetPage(url, waitForXPath: "//a[@download]");
        if (response is ErrorResponse errorResponse)
        {
            throw new Exception($"Error retrieving video page: {errorResponse.Error}");
        }

        const int maxAttempts = 4;
        HtmlNode? downloadButton = null;
        for (var i = 0; i < maxAttempts; i++)
        {
            var soup = await Soupify(response);
            downloadButton = soup.SelectSingleNode("//a[@download]"); // First link will be the highest quality
            if (downloadButton is null)
            {
                if (i == maxAttempts - 1)
                {
                    throw new RipperException("Failed to find download button after multiple attempts.");
                }

                await Sleep(250);
            }
            else
            {
                break;
            }
        }

        // Safety: downloadButton should never be null here due to the loop above
        var videoUrl = downloadButton!.GetAttributeValue("data-url");
        return videoUrl;
    }
}