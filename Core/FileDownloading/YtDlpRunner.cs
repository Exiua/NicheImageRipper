using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using Serilog;

namespace NicheImageRipper.Core.FileDownloading;

public static class YtDlpRunner
{
    private const string YoutubeCookiesFile = "yt_cookies.txt";

    private static readonly ILogger Logger = Log.ForContext(typeof(YtDlpRunner));

    /// <summary>
    ///     Runs yt-dlp against the given link. If the initial run fails and <paramref name="onFailure"/> is
    ///     provided, it's invoked with the captured output/error to decide whether/how to retry (e.g. a
    ///     YouTube-specific age-restriction-cookie retry): returning a replacement command to re-run, or
    ///     null to accept the original failure.
    /// </summary>
    public static async Task<bool> RunYtDlp(FileLink link, string path, string startMessage, string endMessage,
                                            Func<string?, string?, string[]?>? onFailure = null,
                                            CancellationToken cancellationToken = default)
    {
        if (!NicheImageRipper.AvailableFeatures.HasFlag(ExternalFeatureSupport.YtDlp))
        {
            throw new FeatureNotAvailableException(ExternalFeatureSupport.YtDlp);
        }

        var url = link.Url;
        var parent = Directory.GetParent(path)!.FullName;
        var filename = Path.GetFileName(path);
        string[] cmd = link.Referer != ""
            ?
            [
                "--force-overwrites", "-P", $"\"{parent}\"", "-o", $"\"{filename}\"",
                "--add-headers", $"\"Referer:{link.Referer}\"", $"\"{url}\"",
            ]
            :
            [
                "--force-overwrites", "-P", $"\"{parent}\"", "-o", $"\"{filename}\"", $"\"{url}\"",
            ];

        Logger.Debug("yt-dlp {cmd}", string.Join(" ", cmd));
        var (exitCode, output, error) = await ProcessRunner.RunSubprocess("yt-dlp", cmd, true, true,
            startMessage: startMessage, endMessage: endMessage, cancellationToken: cancellationToken);

        if (exitCode == 0 || onFailure is null)
        {
            if (exitCode != 0)
            {
                Logger.Error("Failed to run yt-dlp: {ExitCode}", exitCode);
            }

            return exitCode == 0;
        }

        var retryCmd = onFailure(output, error);
        if (retryCmd is null)
        {
            Logger.Error("Failed to run yt-dlp: {ExitCode}", exitCode);
            return false;
        }

        (exitCode, _, _) = await ProcessRunner.RunSubprocess("yt-dlp", retryCmd, true, true,
            startMessage: startMessage, endMessage: endMessage, cancellationToken: cancellationToken);

        if (exitCode != 0)
        {
            Logger.Error("Failed to run yt-dlp: {ExitCode}", exitCode);
        }

        return exitCode == 0;
    }
}