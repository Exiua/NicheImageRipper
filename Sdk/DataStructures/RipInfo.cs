using System.Collections;
using JetBrains.Annotations;
using NicheImageRipper.Sdk.Common.ExtensionMethods;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.Sdk.Utility;
using Serilog;

namespace NicheImageRipper.Sdk.DataStructures;

/// <summary>
///     Represents the information necessary for ripping images from a website, including the list of image links, the directory name, and the filename scheme.
/// </summary>
public class RipInfo : IEnumerable<FileLink>, IEnumerable<StringFileLinkWrapper>
{
    /// <summary>
    ///     The maximum length allowed for a directory name. If the provided directory name exceeds this length, it will be truncated.
    /// </summary>
    private const int MaxDirectoryNameLength = 200;

    private readonly ILogger _logger = Log.ForContext<RipInfo>();

    public FilenameScheme FilenameScheme { get; set; } = FilenameScheme.Original;

    public List<FileLink> Urls { get; set; } = null!;

    public DownloadMode DownloadMode { get; set; } = DownloadMode.List;

    public int NumUrls { get; set; }
    public int? MaxConcurrentDownloads { get; set; } // null = sequential; set by a parser that knows its downloads are slow/independent

    public string DirectoryName
    {
        get;
        set => field = FilesystemUtility.CleanDirectoryName(value, MaxDirectoryNameLength);
    } = null!; // Initialized through the property setter
    
    public bool MustGenerateManually => DownloadMode == DownloadMode.Generate;

    public static RipInfo Empty => new([]);

    [UsedImplicitly]
    public RipInfo()
    {
    }

    /// <remarks>
    ///     Synchronous: when a URL matches an <see cref="IExpandingSiteLinkRule"/> (e.g. a Google Drive folder), the
    ///     expansion blocks the calling thread. Parsers that may produce such links should use the async factories.
    /// </remarks>
    private RipInfo(List<StringFileLinkWrapper> urls, string directoryName = "",
                    FilenameScheme filenameScheme = FilenameScheme.Original,
                    DownloadMode downloadMode = DownloadMode.List, int numUrls = 0, List<string>? filenames = null, bool discardBlobs = false,
                    string? referer = "", int? maxConcurrentDownloads = null)
        : this(directoryName, filenameScheme, downloadMode, maxConcurrentDownloads)
    {
        // GetAwaiter().GetResult() rather than .Result, so failures surface as their own exception type (e.g.
        // UrlExpiredException) instead of an AggregateException that callers can't match on
        SetUrls(ConvertUrls(urls, discardBlobs, filenames, referer).GetAwaiter().GetResult(), numUrls);
    }

    /// <summary>Sets everything except the links, which the caller fills in with <see cref="SetUrls"/>.</summary>
    private RipInfo(string directoryName, FilenameScheme filenameScheme, DownloadMode downloadMode,
                    int? maxConcurrentDownloads)
    {
        FilenameScheme = filenameScheme;
        DirectoryName = directoryName;
        DownloadMode = downloadMode;
        MaxConcurrentDownloads = maxConcurrentDownloads;
    }

    private void SetUrls(List<FileLink> urls, int numUrls)
    {
        Urls = urls;
        NumUrls = MustGenerateManually ? numUrls : Urls.Count;
    }

    private async Task<List<FileLink>> ConvertUrls(List<StringFileLinkWrapper> urls, bool discardBlobs,
                                                   List<string>? filenames, string? referer)
    {
        try
        {
            return await ConvertUrlsToFileLink(urls, discardBlobs, filenames, referer);
        }
        catch (Exception)
        {
            _logger.Debug("Failed to convert urls to file links: {@urls}", urls);
            throw;
        }
    }

    private static async Task<RipInfo> CreateAsync(List<StringFileLinkWrapper> urls, string directoryName,
                                                   FilenameScheme filenameScheme, List<string>? filenames = null,
                                                   string? referer = "", int? maxConcurrentDownloads = null)
    {
        var ripInfo = new RipInfo(directoryName, filenameScheme, DownloadMode.List, maxConcurrentDownloads);
        ripInfo.SetUrls(await ripInfo.ConvertUrls(urls, false, filenames, referer), 0);
        return ripInfo;
    }

    private RipInfo(List<FileLink> urls, string directoryName, FilenameScheme filenameScheme)
    {
        _logger = Log.ForContext<RipInfo>();
        FilenameScheme = filenameScheme;
        DirectoryName = directoryName;
        Urls = urls;
        DownloadMode = DownloadMode.List;
        NumUrls = Urls.Count;
    }

    // Mainly used for e-hentai to allow partial parsing, where some links are invalid and need to be "re-generated"
    public static RipInfo GenerateWithInvalid(List<FileLink> urls, string directoryName,
                                              FilenameScheme filenameScheme)
    {
        return new RipInfo(urls, directoryName, filenameScheme);
    }

    public static RipInfo FromGenerateInfo(StringFileLinkWrapper baseUrl, string dirName, int numUrls)
    {
        return new RipInfo([baseUrl], dirName, downloadMode: DownloadMode.Generate, numUrls: numUrls);
    }

    public static RipInfo ForExternalTool(StringFileLinkWrapper url, string dirName, FilenameScheme filenameScheme)
    {
        return new RipInfo([url], dirName, filenameScheme, downloadMode: DownloadMode.ExternalTool);
    }

    [Obsolete("Blocks while links such as Google Drive folders are expanded. Use FromUrlListAsync.")]
    public static RipInfo FromUrlList(List<StringFileLinkWrapper> urls, string dirName, FilenameScheme filenameScheme,
                                      bool nameReuse = false, string? referer = "", int? maxConcurrentDownloads = null)
    {
        // Some sites reuse names within subgroups (e.g., images in a chapter will always start with the same name)
        return nameReuse
            ? new RipInfo(urls, dirName, filenameScheme == FilenameScheme.Original
                ? FilenameScheme.Chronological
                : filenameScheme)
            : new RipInfo(urls, dirName, filenameScheme, referer: referer, maxConcurrentDownloads: maxConcurrentDownloads);
    }

    [Obsolete("Blocks while links such as Google Drive folders are expanded. Use FromUrlListWithFilenamesAsync.")]
    public static RipInfo FromUrlListWithFilenames(List<StringFileLinkWrapper> urls, string dirName,
                                                   FilenameScheme filenameScheme, List<string> filenames,
                                                   string? referer = "")
    {
        return new RipInfo(urls, dirName, filenameScheme, filenames: filenames, referer: referer);
    }

    /// <summary>Async <see cref="FromUrlList"/>: expands links such as Google Drive folders without blocking.</summary>
    public static Task<RipInfo> FromUrlListAsync(List<StringFileLinkWrapper> urls, string dirName,
                                                 FilenameScheme filenameScheme, bool nameReuse = false,
                                                 string? referer = "", int? maxConcurrentDownloads = null)
    {
        // Some sites reuse names within subgroups (e.g., images in a chapter will always start with the same name)
        return nameReuse
            ? CreateAsync(urls, dirName,
                filenameScheme == FilenameScheme.Original ? FilenameScheme.Chronological : filenameScheme)
            : CreateAsync(urls, dirName, filenameScheme, referer: referer, maxConcurrentDownloads: maxConcurrentDownloads);
    }

    /// <summary>Async <see cref="FromUrlListWithFilenames"/>: expands links such as Google Drive folders without blocking.</summary>
    public static Task<RipInfo> FromUrlListWithFilenamesAsync(List<StringFileLinkWrapper> urls, string dirName,
                                                              FilenameScheme filenameScheme, List<string> filenames,
                                                              string? referer = "")
    {
        return CreateAsync(urls, dirName, filenameScheme, filenames, referer);
    }

    public RipInfo WithDirectoryName(string directoryName)
    {
        DirectoryName = directoryName;
        return this;
    }

    public IEnumerable<StringFileLinkWrapper> GetStringFileLinks()
    {
        return Urls.ToStringFileLinks();
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
                /*try
                {
                    var (expandedLinks, newLinkCounter) =
                        await expandingRule.ExpandAsync(url.Url, linkCounter, FilenameScheme);
                    fileLinks.AddRange(expandedLinks);
                    linkCounter = newLinkCounter;
                }
                catch (GoogleApiException)
                {
                    // pass
                }*/
                
                var (expandedLinks, newLinkCounter) =
                    await expandingRule.ExpandAsync(url.Url, linkCounter, FilenameScheme);
                fileLinks.AddRange(expandedLinks);
                linkCounter = newLinkCounter;
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

    public IEnumerator<FileLink> GetEnumerator()
    {
        return Urls.GetEnumerator();
    }
    
    IEnumerator<FileLink> IEnumerable<FileLink>.GetEnumerator()
    {
        return GetEnumerator();
    }

    IEnumerator<StringFileLinkWrapper> IEnumerable<StringFileLinkWrapper>.GetEnumerator()
    {
        return Urls.ToStringFileLinks().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public override string ToString()
    {
        return $"([{string.Join(", ", Urls.Select(url => url.ToString()))}], {NumUrls}, {DirectoryName})";
    }
}