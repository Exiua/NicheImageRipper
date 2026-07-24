using Google;
using JetBrains.Annotations;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Core.SiteParsing.LinkRules;
using NicheImageRipper.Core.Utility;
using Serilog;

namespace NicheImageRipper.Core.DataStructures;

/// <summary>
///     Represents the information necessary for ripping images from a website, including the list of image links, the directory name, and the filename scheme.
/// </summary>
public class RipInfo
{
    /// <summary>
    ///     The maximum length allowed for a directory name. If the provided directory name exceeds this length, it will be truncated.
    /// </summary>
    private const int MaxDirectoryNameLength = 200;

    private readonly ILogger _logger = Log.ForContext<RipInfo>();

    public FilenameScheme FilenameScheme { get; set; } = FilenameScheme.Original;

    public List<FileLink> Urls { get; set; } = null!;

    public bool MustGenerateManually { get; set; }

    public int NumUrls { get; set; }

    public string DirectoryName
    {
        get;
        set => field = FilesystemUtility.CleanDirectoryName(value, MaxDirectoryNameLength);
    } = null!; // Initialized through the property setter

    public static RipInfo Empty => new([]);

    [UsedImplicitly]
    public RipInfo()
    {
    }

    private RipInfo(List<StringFileLinkWrapper> urls, string directoryName = "",
                    FilenameScheme filenameScheme = FilenameScheme.Original,
                    bool generate = false, int numUrls = 0, List<string>? filenames = null, bool discardBlobs = false,
                    string? referer = "")
    {
        // SaveRawUrls(urls);
        FilenameScheme = filenameScheme;
        DirectoryName = directoryName;
        try
        {
            Urls = ConvertUrlsToFileLink(urls, discardBlobs, filenames, referer).Result;
        }
        catch (Exception)
        {
            _logger.Debug("Failed to convert urls to file links: {@urls}", urls);
            throw;
        }

        MustGenerateManually = generate;
        NumUrls = generate ? numUrls : Urls.Count;
    }

    private RipInfo(List<FileLink> urls, string directoryName, FilenameScheme filenameScheme)
    {
        _logger = Log.ForContext<RipInfo>();
        FilenameScheme = filenameScheme;
        DirectoryName = directoryName;
        Urls = urls;
        MustGenerateManually = false;
        NumUrls = Urls.Count;
    }

    // Mainly used for e-hentai to allow partial parsing, where some links are invalid and need to be "re-generated"
    internal static RipInfo GenerateWithInvalid(List<FileLink> urls, string directoryName,
                                                FilenameScheme filenameScheme)
    {
        return new RipInfo(urls, directoryName, filenameScheme);
    }

    public static RipInfo FromGenerateInfo(StringFileLinkWrapper baseUrl, string dirName, int numUrls)
    {
        return new RipInfo([baseUrl], dirName, generate: true, numUrls: numUrls);
    }

    public static RipInfo FromUrlList(List<StringFileLinkWrapper> urls, string dirName, FilenameScheme filenameScheme,
                                      bool nameReuse = false, string? referer = "")
    {
        // Some sites reuse names within subgroups (e.g., images in a chapter will always start with the same name)
        return nameReuse
            ? new RipInfo(urls, dirName, filenameScheme == FilenameScheme.Original
                ? FilenameScheme.Chronological
                : filenameScheme)
            : new RipInfo(urls, dirName, filenameScheme, referer: referer);
    }

    public static RipInfo FromUrlListWithFilenames(List<StringFileLinkWrapper> urls, string dirName,
                                                   FilenameScheme filenameScheme, List<string> filenames,
                                                   string? referer = "")
    {
        return new RipInfo(urls, dirName, filenameScheme, filenames: filenames, referer: referer);
    }

    public RipInfo WithDirectoryName(string directoryName)
    {
        DirectoryName = directoryName;
        return this;
    }

    private async Task<List<FileLink>> ConvertUrlsToFileLink(List<StringFileLinkWrapper> urls, bool discardBlob,
                                                             List<string>? filenames = null, string? referer = "")
    {
        var fileLinks = new List<FileLink>();
        var linkCounter = 0; // Current index of fileLinks (used for naming fileLinks when generating numeric names)
        var filenameCounter = 0; // Current index of filenames
        urls = RemoveDuplicates(urls);
        foreach (var url in urls)
        {
            // IsFileLink is the same as url.FileLink is not null
            if (url.IsFileLink)
            {
                var fileLink = url.FileLink;
                if (FilenameScheme == FilenameScheme.Chronological)
                {
                    fileLink.Rename(linkCounter);
                }

                linkCounter++;
                fileLinks.Add(fileLink);
                continue;
            }

            if (SiteLinkRuleRegistry.FindMatch(url.Url) is IExpandingSiteLinkRule expandingRule)
            {
                try
                {
                    var (expandedLinks, newLinkCounter) =
                        await expandingRule.ExpandAsync(url.Url, linkCounter, FilenameScheme);
                    fileLinks.AddRange(expandedLinks);
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
                var fileLink = FileLink.WithFilename(url.Url, filename, FilenameScheme, index: linkCounter,
                    referer: referer);
                fileLinks.Add(fileLink);
                linkCounter++;
            }
        }

        if (discardBlob)
        {
            fileLinks = fileLinks.Where(fl => !fl.IsBlob).ToList();
        }

        return fileLinks;
    }

    private List<StringFileLinkWrapper> RemoveDuplicates(List<StringFileLinkWrapper> urls)
    {
        var urlSet = new HashSet<string>();
        var newUrls = new List<StringFileLinkWrapper>();
        foreach (var url in urls)
        {
            if ((url.Url is not null && urlSet.Add(url.Url)) ||
                (url.FileLink is not null && urlSet.Add(url.FileLink.Url)))
            {
                newUrls.Add(url);
            }
            else
            {
                _logger.Debug("Duplicate url: {Url}", url);
            }
        }

        return newUrls;
    }

    public override string ToString()
    {
        return $"([{string.Join(", ", Urls.Select(url => url.ToString()))}], {NumUrls}, {DirectoryName})";
    }
}