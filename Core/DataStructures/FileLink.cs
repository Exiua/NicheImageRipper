using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Web;
using JetBrains.Annotations;
using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.SiteParsing.LinkRules;
using NicheImageRipper.Core.Utility;
using Serilog;

namespace NicheImageRipper.Core.DataStructures;

public partial class FileLink
{
    private ILogger Logger { get; } = Log.ForContext<FileLink>();

    public string? Referer { get; set; }
    public LinkInfo LinkInfo { get; set; } = LinkInfo.None;
    public string Url { get; set; } = null!;
    public string Filename { get; set; } = null!;

    [JsonIgnore] public bool IsBlob => Url.StartsWith("blob:");

    [JsonIgnore] public bool IsInvalid => Url == "";

    [JsonIgnore]
    [MemberNotNullWhen(true, nameof(Referer))]
    public bool HasReferer => !string.IsNullOrEmpty(Referer);

    public static FileLink Invalid => new()
    {
        Referer = null,
        LinkInfo = LinkInfo.None,
        Url = "",
        Filename = "",
    };

    // Only used for (de)serialization
    [UsedImplicitly]
    public FileLink()
    {
    }

    /// <summary>
    ///     Construct an ImageLink from a URL and filename scheme
    /// </summary>
    /// <param name="url">URL of the file</param>
    /// <param name="filenameScheme">Scheme to use for generating the filename</param>
    /// <param name="index">Index of the file in the list of files (used for chronological naming)</param>
    /// <param name="filename">Optional filename to use instead of generating one</param>
    /// <param name="linkInfo">Optional LinkInfo to set instead of auto-detecting</param>
    /// <param name="referer">Optional referer to set for the link</param>
    /// <param name="cleanFilename">
    ///     Whether to clean the provided filename or not (default: false).
    ///     Does nothing if <paramref name="filename"/> was not provided.
    /// </param>
    private FileLink(string url, FilenameScheme filenameScheme, int index, string filename = "",
                     LinkInfo linkInfo = LinkInfo.None, string? referer = "", bool cleanFilename = false)
    {
        url = HttpUtility.HtmlDecode(url.Trim());

        if (url.StartsWith("text:"))
        {
            LinkInfo = LinkInfo.Text;
            Url = url[5..];
            Filename = "urls.txt";
            return;
        }

        url = url.Replace("\n", "");

        var siteResult = SiteLinkRuleRegistry.FindMatch(url)?.Resolve(url);

        Url = siteResult?.Url ?? (url.StartsWith("//") ? $"https:{url}" : url);
        Referer = siteResult?.Referer ?? (referer == "" ? null : referer);
        LinkInfo = siteResult?.LinkInfo ?? linkInfo;

        Filename = GenerateFilename(Url, filenameScheme, index, filename, cleanFilename, siteResult?.Filename);
    }

    public static FileLink Create(
        string url,
        FilenameScheme filenameScheme,
        int index = 0,
        LinkInfo linkInfo = LinkInfo.None,
        string? referer = null)
    {
        return CreateCore(
            url,
            filenameScheme,
            index,
            suppliedFilename: null,
            resolveFilenameDuringDownload: false,
            linkInfo,
            referer,
            cleanFilename: false);
    }

    public static FileLink WithFilename(
        string url,
        string filename,
        FilenameScheme filenameScheme,
        int index = 0,
        LinkInfo linkInfo = LinkInfo.None,
        string? referer = null,
        bool cleanFilename = false)
    {
        return CreateCore(
            url,
            filenameScheme,
            index,
            suppliedFilename: filename,
            resolveFilenameDuringDownload: false,
            linkInfo,
            referer,
            cleanFilename);
    }

    public static FileLink WithDownloadResolvedFilename(
        string url,
        FilenameScheme filenameScheme,
        LinkInfo linkInfo = LinkInfo.None,
        string? referer = null)
    {
        return CreateCore(
            url,
            FilenameScheme.Original,
            index: 0,
            suppliedFilename: null,
            resolveFilenameDuringDownload: true,
            linkInfo,
            referer,
            cleanFilename: false);
    }

    private static FileLink CreateCore(
        string url,
        FilenameScheme filenameScheme,
        int index,
        string? suppliedFilename,
        bool resolveFilenameDuringDownload,
        LinkInfo linkInfo,
        string? referer,
        bool cleanFilename)
    {
        // Useful shape for later refactoring
        return new FileLink(url, filenameScheme, index, filename: suppliedFilename ?? "", linkInfo: linkInfo,
            referer: referer ?? "", cleanFilename: cleanFilename);
    }

    public void Rename(int index)
    {
        var ext = Path.GetExtension(Filename);
        Filename = index + ext;
    }

    public void Rename(string newStem)
    {
        var ext = Path.GetExtension(Filename);
        Filename = newStem + ext;
    }

    public bool Contains(string url)
    {
        return Url.Contains(url);
    }

    public void RegenerateFilename(FilenameScheme filenameScheme, int index)
    {
        Filename = GenerateFilename(Url, filenameScheme, index, "", false, null);
    }

    private string GenerateFilename(string url, FilenameScheme filenameScheme, int index, string filename,
                                    bool cleanFilename, string? siteFilename)
    {
        var filenameProvided = filename != "";

        if (!filenameProvided)
        {
            // GDrive filenames come from the Drive API and are expected to be supplied by the caller —
            // deliberately skip the generic URI-based fallback for this one case.
            filename = LinkInfo == LinkInfo.GDrive
                ? ""
                : FinalizeFilename(url, siteFilename ?? ExtractFallbackFilename(url));
        }
        else if (LinkInfo == LinkInfo.M3U8YtDlp)
        {
            var extension = Path.GetExtension(filename);
            if (extension != "")
            {
                filename = Path.GetFileNameWithoutExtension(filename); // yt-dlp adds the extension automatically
            }
        }

        if (filenameScheme == FilenameScheme.Original)
        {
            if (filename.Contains('%'))
            {
                filename = Uri.UnescapeDataString(filename);
            }

            if (cleanFilename && filenameProvided)
            {
                var cleaned = FilesystemUtility.CleanPathStem(filename);
                filename = cleaned == "" ? FinalizeFilename(url, "", wasCleanedToEmpty: true) : cleaned;
            }

            return filename;
        }

        var ext = Path.GetExtension(filename);
        // Missing case is unreachable
        // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
        return filenameScheme switch
        {
            FilenameScheme.Hash => StringUtility.HashStringMd5(url) + ext,
            FilenameScheme.Chronological => index + ext,
            _ => throw new RipperException($"FilenameScheme out of bounds: {filenameScheme}"),
        };
    }

    /// <summary>
    /// Generic path-based filename derivation for URLs no site rule matched.
    /// </summary>
    private string ExtractFallbackFilename(string url)
    {
        var linkInfoSet = LinkInfo != LinkInfo.None;

        string localPath;
        try
        {
            localPath = new Uri(url).LocalPath;
        }
        catch (UriFormatException)
        {
            Logger.Error("Invalid URL format: {Url}", url);
            throw;
        }

        var fileName = Path.GetFileName(localPath);
        if (url.Contains(".m3u8"))
        {
            if (!linkInfoSet)
            {
                LinkInfo = LinkInfo.M3U8Ffmpeg;
            }

            fileName = fileName.Replace(".m3u8", ".mp4");
        }

        return fileName;
    }

    /// <summary>
    /// Shared cleanup applied to any filename — whether site-rule-derived or the generic fallback —
    /// mirroring the tail of the original ExtractFilename, which ran for every branch.
    /// </summary>
    private string FinalizeFilename(string url, string fileName, bool wasCleanedToEmpty = false)
    {
        if (fileName == "")
        {
            if (wasCleanedToEmpty)
            {
                Logger.Warning("Filename was cleaned to empty: {Url}", url);
            }
            else
            {
                Logger.Warning("No file name provided: {Url}", url);
            }
            
            return Guid.NewGuid().ToString();
        }

        return FilesystemUtility.CleanPathStem(fileName);
    }

    public override string ToString()
    {
        var linkInfo = Enum.GetName(LinkInfo);
        var url = LinkInfo == LinkInfo.Base64 ? UrlUtility.TruncateLongUrl(Url) : Url;
        return !Referer.IsNullOrEmpty()
            ? $"({url}, {Filename}, {Referer}, {linkInfo})"
            : $"({url}, {Filename}, {linkInfo})";
    }
}