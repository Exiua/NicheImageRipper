using System.Text.RegularExpressions;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public partial class MangaDexParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "mangadex";
    public static string[] SupportedUrls => ["https://mangadex.org/"];

    public MangaDexParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<MangaDexParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for mangadex.org and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        const int delay = 250;
        const int maxRetries = 4;
        // TODO: Support other languages
        var match = MangaDexRegex().Match(CurrentUrl);
        if (!match.Success)
        {
            throw new RipperException("Unable to parse manga id");
        }

        var mangaId = match.Groups[1].Value;
        Logger.Debug("Manga ID: {mangaId}", mangaId);
        var client = new MangaDexClient();
        var response = await client.Manga.GetMetadata(mangaId, cancellationToken);
        if (response is not MangaMetadataResponse mangaMetadata)
        {
            var errorResponse = (ErrorResponse)response;
            var exception = new RipperException("Unable to get manga metadata");
            Logger.Error("Error: {error}", errorResponse.Errors.First().Detail);
            throw exception;
        }

        var metadataAttributes = mangaMetadata.Data.Attributes;
        string dirName;
        if (metadataAttributes.Title.TryGetValue("en", out var value))
        {
            dirName = value;
        }
        else
        {
            var foundEnTitle = metadataAttributes.AltTitles.Any(altTitle => altTitle.TryGetValue("en", out value));
            dirName = foundEnTitle ? value! : metadataAttributes.Title.First().Value;
        }

        response = await client.Manga.GetVolumeAndChapter(mangaId, cancellationToken);
        if (response is not AggregateMangaResponse manga)
        {
            var errorResponse = (ErrorResponse)response;
            var exception = new RipperException("Unable to get manga volume and chapter");
            Logger.Error("Error: {error}", errorResponse.Errors.First().Detail);
            throw exception;
        }

        var images = new List<StringFileLinkWrapper>();
        var mangaImages = new List<List<StringFileLinkWrapper>>();
        var volumes = manga.Volumes;
        foreach (var(volumeLabel, volumeData)in volumes)
        {
            Logger.Debug("Volume {volumeLabel}", volumeLabel);
            var chapters = volumeData.Chapters;
            foreach (var(chapterLabel, chapterData)in chapters)
            {
                var chapterIds = new Guid[chapterData.Others.Length + 1];
                chapterIds[0] = chapterData.Id;
                for (var i = 0; i < chapterData.Others.Length; i++)
                {
                    chapterIds[i + 1] = chapterData.Others[i];
                }

                var found = false;
                foreach (var chapterId in chapterIds)
                {
                    await Task.Delay(delay, cancellationToken);
                    Logger.Debug("Chapter {chapterLabel} ID: {chapterId}", chapterLabel, chapterId);
                    response = await client.Chapter.GetChapter(chapterId, cancellationToken);
                    if (response is not ChapterResponse chapter)
                    {
                        var errorResponse = (ErrorResponse)response;
                        var exception = new RipperException("Unable to get chapter");
                        Logger.Error("Error: {error}", errorResponse.Errors.First().Detail);
                        throw exception;
                    }

                    var chapterAttributes = chapter.Data.Attributes;
                    if (chapterAttributes.TranslatedLanguage != "en")
                    {
                        continue;
                    }

                    found = true;
                    AtHomeResponse atHome = null!;
                    for (var i = 0; i < maxRetries; i++)
                    {
                        response = await client.AtHome.GetServerUrls(chapterId, cancellationToken);
                        if (response is ErrorResponse errorResponse)
                        {
                            if (i != maxRetries - 1)
                            {
                                await Task.Delay(delay * 4, cancellationToken);
                                continue;
                            }

                            var exception = new RipperException("Unable to get MangaDex@Home server urls");
                            Logger.Error("Error: {error}", errorResponse.Errors.First().Detail);
                            throw exception;
                        }

                        atHome = (AtHomeResponse)response;
                        break;
                    }

                    // Safety: atHome should never be null here as we throw an exception in the loop if it is
                    var serverUrls = atHome.Chapter;
                    var baseUrl = atHome.BaseUrl;
                    var hash = serverUrls.Hash;
                    var chapterImages = new List<StringFileLinkWrapper>();
                    foreach (var(i, page)in serverUrls.Data.Enumerate())
                    {
                        var ext = Path.GetExtension(page);
                        var url = $"{baseUrl}/data/{hash}/{page}";
                        var filename = volumeLabel == "none" ? $"{chapterLabel}-{i + 1}{ext}" : $"{volumeLabel}-{chapterLabel}-{i + 1}{ext}";
                        var fileLink = FileLink.WithFilename(url, filename, FilenameScheme);
                        chapterImages.Add(fileLink);
                    }

                    mangaImages.Add(chapterImages);
                    break;
                }

                if (!found)
                {
                    Logger.Warning("No English chapter found for Manga {mangaId} Vol. {volumeLabel} Ch. {chapterLabel}", mangaId, volumeLabel, chapterLabel);
                }
            }
        }

        // Order received is newest to oldest, so we reverse it
        foreach (var chapterImages in mangaImages.AsEnumerable().Reverse())
        {
            images.AddRange(chapterImages);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    [GeneratedRegex(@"/title/([^/]+)")]
    private static partial Regex MangaDexRegex();
}