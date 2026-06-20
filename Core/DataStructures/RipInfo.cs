using Google;
using JetBrains.Annotations;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.FileDownloading;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Core.Utility;
using Serilog;

namespace NicheImageRipper.Core.DataStructures;

public class RipInfo
{
    private const int MaxDirectoryNameLength = 200;
    
    private readonly ILogger Logger;

    public FilenameScheme FilenameScheme { get; set; } = FilenameScheme.Original;

    public List<ImageLink> Urls { get; set; } = null!;

    public bool MustGenerateManually { get; set; }

    public int NumUrls { get; set; }

    public string DirectoryName
    {
        get;
        set => field = CleanDirectoryName(value);
    } = null!; // Initialized through the property setter

    public static RipInfo Empty => new([]);

    [UsedImplicitly]
    public RipInfo()
    {
        Logger = Log.ForContext<RipInfo>();
    }

    private RipInfo(List<StringImageLinkWrapper> urls, string directoryName = "",
                    FilenameScheme filenameScheme = FilenameScheme.Original,
                    bool generate = false, int numUrls = 0, List<string>? filenames = null, bool discardBlobs = false,
                    string? referer = "")
    {
        // SaveRawUrls(urls);
        Logger = Log.ForContext<RipInfo>();
        FilenameScheme = filenameScheme;
        DirectoryName = directoryName;
        try
        {
            Urls = ConvertUrlsToImageLink(urls, discardBlobs, filenames, referer: referer).Result;
        }
        catch (Exception)
        {
            Logger.Debug("Failed to convert urls to image links: {@urls}", urls);
            throw;
        }

        MustGenerateManually = generate;
        NumUrls = generate ? numUrls : Urls.Count;
    }

    private RipInfo(List<ImageLink> urls, string directoryName, FilenameScheme filenameScheme)
    {
        Logger = Log.ForContext<RipInfo>();
        FilenameScheme = filenameScheme;
        DirectoryName = directoryName;
        Urls = urls;
        MustGenerateManually = false;
        NumUrls = Urls.Count;
    }

    // Mainly used for e-hentai to allow partial parsing, where some links are invalid and need to be "re-generated"
    internal static RipInfo GenerateWithInvalid(List<ImageLink> urls, string directoryName,
                                                FilenameScheme filenameScheme)
    {
        return new RipInfo(urls, directoryName, filenameScheme);
    }

    public static RipInfo FromGenerateInfo(StringImageLinkWrapper baseUrl, string dirName, int numUrls)
    {
        return new RipInfo([baseUrl], dirName, generate: true, numUrls: numUrls);
    }

    public static RipInfo FromUrlList(List<StringImageLinkWrapper> urls, string dirName, FilenameScheme filenameScheme,
                                      bool nameReuse = false, string? referer = "")
    {
        // Some sites reuse names within subgroups (e.g., images in a chapter will always start with the same name)
        return nameReuse
            ? new RipInfo(urls, dirName, filenameScheme == FilenameScheme.Original
                ? FilenameScheme.Chronological
                : filenameScheme)
            : new RipInfo(urls, dirName, filenameScheme, referer: referer);
    }

    public static RipInfo FromUrlListWithFilenames(List<StringImageLinkWrapper> urls, string dirName,
                                                   FilenameScheme filenameScheme, List<string> filenames, string? referer = "")
    {
        return new RipInfo(urls, dirName, filenameScheme, filenames: filenames, referer: referer);
    }

    public RipInfo WithDirectoryName(string directoryName)
    {
        DirectoryName = directoryName;
        return this;
    }

    private async Task<List<ImageLink>> ConvertUrlsToImageLink(List<StringImageLinkWrapper> urls, bool discardBlob,
                                                               List<string>? filenames = null, string? referer = "")
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

            if (url.Url.Contains("drive.google.com"))
            {
                try
                {
                    var (imageLink, newLinkCounter) = await GDriveHelper.QueryGDriveLinks(url.Url, linkCounter, FilenameScheme);
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
                var imageLink = new ImageLink(url.Url, FilenameScheme, linkCounter, filename: filename, referer: referer);
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

    private List<StringImageLinkWrapper> RemoveDuplicates(List<StringImageLinkWrapper> urls)
    {
        var urlSet = new HashSet<string>();
        var newUrls = new List<StringImageLinkWrapper>();
        foreach (var url in urls)
        {
            if (url.Url is not null && urlSet.Add(url.Url) ||
                url.ImageLink is not null && urlSet.Add(url.ImageLink.Url))
            {
                newUrls.Add(url);
            }
            else
            {
                Logger.Debug("Duplicate url: {Url}", url);
            }
        }

        return newUrls;
    }

    private string CleanDirectoryName(string directoryName)
    {
        var name = string.IsNullOrWhiteSpace(directoryName) ? Guid.NewGuid().ToString() : FilesystemUtility.CleanPathStem(directoryName);
        if (name.Length <= MaxDirectoryNameLength)
        {
            return name;
        }

        Logger.Warning("Directory name too long (length: {Length}). Truncating to {MaxLength} characters.",
            name.Length, MaxDirectoryNameLength);
        name = name[..MaxDirectoryNameLength].Trim();

        return name;
    }

    public override string ToString()
    {
        return $"([{string.Join(", ", Urls.Select(url => url.ToString()))}], {NumUrls}, {DirectoryName})";
    }
}