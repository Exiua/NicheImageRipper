using System.Text.RegularExpressions;
using MangaDexLibrary;
using MangaDexLibrary.Responses;
using NicheImageRipper.Sdk.Common.ExtensionMethods;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.Exceptions;
using NicheImageRipper.Sdk.SiteParsing;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.MangaDex;

public partial class MangaDexParser : HtmlParser, IHtmlParser, ICustomSettingsHtmlParser
{
    public static string ParserName => "mangadex";
    public static Type CustomSettingsType => typeof(MangaDexConfig);
    public static string[] SupportedUrls => ["https://mangadex.org/"];

    public MangaDexParser(WebDriver driver, Dictionary<string, string> requestHeaders,
                          FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders,
        IHtmlParser.GetFilenameScheme<MangaDexParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for mangadex.org and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        const int delay = 250;
        const int maxRetries = 4;
        var language = MangaDexConfig.From(Config.GetSiteConfig(ParserName)?.Custom).Language;
        Logger.Debug("Chapter language: {Language}", language);
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
        var dirName = ChooseTitle(metadataAttributes.Title, metadataAttributes.AltTitles, language);
        var availableLanguages = metadataAttributes.AvailableTranslatedLanguages ?? [];
        if (availableLanguages.Length > 0 &&
            !availableLanguages.Contains(language, StringComparer.OrdinalIgnoreCase))
        {
            Logger.Warning("Manga {MangaId} has no {Language} translation; available: {Available}", mangaId, language,
                string.Join(", ", availableLanguages));
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
        foreach (var (volumeLabel, volumeData)in volumes)
        {
            Logger.Debug("Volume {volumeLabel}", volumeLabel);
            var chapters = volumeData.Chapters;
            foreach (var (chapterLabel, chapterData)in chapters)
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
                    if (!string.Equals(chapterAttributes.TranslatedLanguage, language, StringComparison.OrdinalIgnoreCase))
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
                    foreach (var (i, page)in serverUrls.Data.Enumerate())
                    {
                        var ext = Path.GetExtension(page);
                        var url = $"{baseUrl}/data/{hash}/{page}";
                        var filename = volumeLabel == "none"
                            ? $"{chapterLabel}-{i + 1}{ext}"
                            : $"{volumeLabel}-{chapterLabel}-{i + 1}{ext}";
                        var fileLink = FileLink.WithFilename(url, filename, FilenameScheme);
                        chapterImages.Add(fileLink);
                    }

                    mangaImages.Add(chapterImages);
                    break;
                }

                if (!found)
                {
                    Logger.Warning("No {Language} chapter found for Manga {mangaId} Vol. {volumeLabel} Ch. {chapterLabel}",
                        language, mangaId, volumeLabel, chapterLabel);
                }
            }
        }

        // Order received is newest to oldest, so we reverse it
        foreach (var chapterImages in mangaImages.AsEnumerable().Reverse())
        {
            images.AddRange(chapterImages);
        }

        return await RipInfo.FromUrlListAsync(images, dirName, FilenameScheme);
    }

    /// <summary>
    ///     Picks the folder name: the title in <paramref name="language"/>, then in English, then whatever title the
    ///     manga has. Main titles win over alternative titles in the same language.
    /// </summary>
    public static string ChooseTitle(IReadOnlyDictionary<string, string> title,
                                     IEnumerable<Dictionary<string, string>>? altTitles, string language)
    {
        var alternatives = altTitles?.ToList() ?? [];
        foreach (var candidate in new[] { language, "en" }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (TryGetTitle(title, candidate, out var found))
            {
                return found;
            }

            foreach (var altTitle in alternatives)
            {
                if (TryGetTitle(altTitle, candidate, out found))
                {
                    return found;
                }
            }
        }

        return title.Values.FirstOrDefault() ?? throw new RipperException("Manga has no title");

        static bool TryGetTitle(IReadOnlyDictionary<string, string> titles, string code, out string found)
        {
            foreach (var (key, value) in titles)
            {
                if (key.Equals(code, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(value))
                {
                    found = value;
                    return true;
                }
            }

            found = "";
            return false;
        }
    }

    [GeneratedRegex(@"/title/([^/]+)")]
    private static partial Regex MangaDexRegex();
}