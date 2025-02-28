using System.Text.RegularExpressions;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using MangaDexLibrary;
using MangaDexLibrary.Responses;
using Serilog;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public partial class MangaDexParser : HtmlParser
{
    public MangaDexParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for mangadex.org and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
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
        Log.Debug("Manga ID: {mangaId}", mangaId);
        var client = new MangaDexClient();
        var response = await client.Manga.GetMetadata(mangaId);
        if (response is not MangaMetadataResponse mangaMetadata)
        {
            var errorResponse = (ErrorResponse) response;
            var exception = new RipperException("Unable to get manga metadata");
            Log.Error("Error: {error}", errorResponse.Errors.First().Detail);
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
        
        response = await client.Manga.GetVolumeAndChapter(mangaId);
        if (response is not AggregateMangaResponse manga)
        {
            var errorResponse = (ErrorResponse) response;
            var exception = new RipperException("Unable to get manga volume and chapter");
            Log.Error("Error: {error}", errorResponse.Errors.First().Detail);
            throw exception;
        }
        
        var images = new List<StringImageLinkWrapper>();
        var mangaImages = new List<List<StringImageLinkWrapper>>();
        var volumes = manga.Volumes;
        foreach (var (volumeLabel, volumeData) in volumes)
        {
            Log.Debug("Volume {volumeLabel}", volumeLabel);
            var chapters = volumeData.Chapters;
            foreach (var (chapterLabel, chapterData) in chapters)
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
                    await Task.Delay(delay);
                    Log.Debug("Chapter {chapterLabel} ID: {chapterId}", chapterLabel, chapterId);
                    response = await client.Chapter.GetChapter(chapterId);
                    if (response is not ChapterResponse chapter)
                    {
                        var errorResponse = (ErrorResponse) response;
                        var exception = new RipperException("Unable to get chapter");
                        Log.Error("Error: {error}", errorResponse.Errors.First().Detail);
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
                        response = await client.AtHome.GetServerUrls(chapterId);
                        if (response is ErrorResponse errorResponse)
                        {
                            if (i != maxRetries - 1)
                            {
                                await Task.Delay(delay * 4);
                                continue;
                            }
                            
                            var exception = new RipperException("Unable to get MangaDex@Home server urls");
                            Log.Error("Error: {error}", errorResponse.Errors.First().Detail);
                            throw exception;
                        }
                        
                        atHome = (AtHomeResponse) response;
                        break;
                    }
                    
                    // Safety: atHome should never be null here as we throw an exception in the loop if it is
                    var serverUrls = atHome.Chapter;
                    var baseUrl = atHome.BaseUrl;
                    var hash = serverUrls.Hash;
                    var chapterImages = new List<StringImageLinkWrapper>();
                    foreach (var (i, page) in serverUrls.Data.Enumerate())
                    {
                        var ext  = Path.GetExtension(page);
                        var url = $"{baseUrl}/data/{hash}/{page}";
                        var filename = volumeLabel == "none"
                            ? $"{chapterLabel}-{i+1}{ext}"
                            : $"{volumeLabel}-{chapterLabel}-{i+1}{ext}";
                        var imageLink = new ImageLink(url, FilenameScheme, 0, filename: filename);
                        chapterImages.Add(imageLink);
                    }
                    
                    mangaImages.Add(chapterImages);
                    break;
                }
                
                if (!found)
                {
                    Log.Warning("No English chapter found for Manga {mangaId} Vol. {volumeLabel} Ch. {chapterLabel}", 
                        mangaId, volumeLabel, chapterLabel);
                }
            }
        }
        
        // Order received is newest to oldest, so we reverse it
        foreach (var chapterImages in mangaImages.AsEnumerable().Reverse())
        {
            images.AddRange(chapterImages);
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
    
    [GeneratedRegex(@"/title/([^/]+)")]
    private static partial Regex MangaDexRegex();
}
