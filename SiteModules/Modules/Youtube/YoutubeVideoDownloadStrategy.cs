using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.FileDownloading;
using Serilog;

namespace NicheImageRipper.SiteModules.Modules.Youtube;

public sealed class YoutubeVideoDownloadStrategy : IFileDownloadStrategy
{
    private const string YoutubeCookiesFile = "yt_cookies.txt";
    private static readonly ILogger Logger = Log.ForContext<YoutubeVideoDownloadStrategy>();

    public IEnumerable<LinkInfo> HandlesLinkInfo => [YoutubeLinkInfo.YoutubeVideo];
    public bool SupportsPostProcessing => false;

    public async Task<DownloadResult> DownloadAsync(FileLink link, string imagePath, DownloadContext context,
                                                    CancellationToken cancellationToken = default)
    {
        var success = await YtDlpRunner.RunYtDlp(link, imagePath, "Starting youtube-dl download",
            "youtube-dl download finished", onFailure: (output, _) => TryAgeRestrictedRetry(link, imagePath, output),
            cancellationToken: cancellationToken);
        return success ? DownloadResult.Success() : DownloadResult.Failed();
    }

    private static string[]? TryAgeRestrictedRetry(FileLink link, string path, string? output)
    {
        var lines = (output ?? "").Split("\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!lines.Any(line => line.Contains("This video is age-restricted")))
        {
            return null;
        }

        if (!File.Exists(YoutubeCookiesFile))
        {
            Logger.Error("Video is age-restricted but no cookies file found at {YoutubeCookiesFile}",
                YoutubeCookiesFile);
            return null;
        }

        Logger.Information("Video is age-restricted, trying again with cookies");
        var parent = Directory.GetParent(path)!.FullName;
        var filename = Path.GetFileName(path);
        return
        [
            "--force-overwrites", "--cookies", $"\"{YoutubeCookiesFile}\"",
            "-P", $"\"{parent}\"", "-o", $"\"{filename}\"", $"\"{link.Url}\"",
        ];
    }
}