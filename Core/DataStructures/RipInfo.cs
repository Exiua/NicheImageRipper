using System.Net;
using System.Text;
using Core.Enums;
using Core.FileDownloading;
using Core.SiteParsing;
using Core.Utility;
using Google;
using JetBrains.Annotations;
using Serilog;

namespace Core.DataStructures;

public class RipInfo
{
    private string _directoryName = null!; // Initialized through the property setter

    [UsedImplicitly]
    public FilenameScheme FilenameScheme { get; set; } = FilenameScheme.Original;
    
    [UsedImplicitly]
    public List<ImageLink> Urls { get; set; } = null!;
    
    [UsedImplicitly]
    public bool MustGenerateManually { get; set; }

    [UsedImplicitly]
    public int NumUrls { get; set; }

    [UsedImplicitly]
    public string DirectoryName
    {
        get => _directoryName;
        set => _directoryName = CleanDirectoryName(value);
    }

    [UsedImplicitly]
    public RipInfo()
    {
    }

    public RipInfo(List<StringImageLinkWrapper> urls, string directoryName = "", 
                   FilenameScheme filenameScheme = FilenameScheme.Original,
                   bool generate = false, int numUrls = 0, List<string>? filenames = null, bool discardBlobs = false)
    {
        // SaveRawUrls(urls);
        FilenameScheme = filenameScheme;
        DirectoryName = directoryName;
        try
        {
            Urls = ConvertUrlsToImageLink(urls, discardBlobs, filenames).Result;
        }
        catch (Exception)
        {
            Log.Debug("Failed to convert urls to image links: {@urls}", urls);
            throw;
        }
        
        MustGenerateManually = generate;
        NumUrls = generate ? numUrls : Urls.Count;
    }

    private async Task<List<ImageLink>> ConvertUrlsToImageLink(List<StringImageLinkWrapper> urls, bool discardBlob,
                                                               List<string>? filenames = null)
    {
        var imageLinks = new List<ImageLink>();
        var linkCounter = 0; // Current index of image_links (used for naming image_links when generating numeric names)
        var filenameCounter = 0; // Current index of filenames
        urls = RemoveDuplicates(urls);
        foreach (var url in urls)
        {
            // IsImageLink is the same as url.ImageLink is not null
            if (url.IsImageLink)
            {
                var imageLink = url.ImageLink!;
                if (FilenameScheme == FilenameScheme.Chronological)
                {
                    imageLink.Rename(linkCounter);
                }

                linkCounter++;
                imageLinks.Add(imageLink);
                continue;
            }

            if (url.Url!.Contains("drive.google.com"))
            {
                try
                {
                    var (imageLink, newLinkCounter) = await QueryGDriveLinks(url.Url, linkCounter);
                    imageLinks.AddRange(imageLink);
                    linkCounter = newLinkCounter;
                }
                catch (GoogleApiException) // googleapiclient.errors.HttpError
                {
                    // pass
                }
            }
            else
            {
                var filename = filenames?[filenameCounter] ?? "";
                filenameCounter++;
                var imageLink = new ImageLink(url.Url, FilenameScheme, linkCounter, filename: filename);
                imageLinks.Add(imageLink);
                linkCounter++;
            }
        }

        if (discardBlob)
        {
            imageLinks = imageLinks.Where(imageLink => !imageLink.IsBlob).ToList();
        }

        return imageLinks;
    }

    private async Task<(List<ImageLink>, int)> QueryGDriveLinks(string gDriveUrl, int index)
    {
        var service = await GDriveHelper.AuthenticateGDrive();
        var (id, singleFile) = ExtractId(gDriveUrl);
        var files = await service.GetFiles(id);
        var imageLinks = new List<ImageLink>();
        var counter = index;
        foreach (var file in files)
        {
            var imgLink = new ImageLink(file.Id, FilenameScheme, counter, filename: file.Name,
                linkInfo: LinkInfo.GDrive);
            imageLinks.Add(imgLink);
            counter++;
        }

        return (imageLinks, counter);
    }

    private static (string, bool) ExtractId(string url)
    {
        var parts = url.Split("/");
        if (url.Contains("/d/"))
        {
            return (parts[^2], true);
        }

        var id = parts[^1].Split('?')[0];
        if (id is "open" or "folderview")
        {
            id = parts[^1].Split("?id=")[^1];
        }

        return (id, false);
    }

    private static List<StringImageLinkWrapper> RemoveDuplicates(List<StringImageLinkWrapper> urls)
    {
        var urlSet = new HashSet<string>();
        var newUrls = new List<StringImageLinkWrapper>();
        foreach (var url in urls)
        {
            if (url.Url is not null && urlSet.Add(url.Url))
            {
                newUrls.Add(url);
            }
            else if (url.ImageLink is not null && urlSet.Add(url.ImageLink.Url))
            {
                newUrls.Add(url);
            }
        }

        return newUrls;
    }

    private static string CleanDirectoryName(string directoryName)
    {
        return FilesystemUtility.CleanPathStem(directoryName);
    }

    private static void SaveRawUrls(List<StringImageLinkWrapper> urls)
    {
        JsonUtility.Serialize("raw_urls.json", urls);
    }

    public override string ToString()
    {
        return $"([{string.Join(", ", Urls.Select(url => url.ToString()))}], {NumUrls}, {DirectoryName})";
    }
}