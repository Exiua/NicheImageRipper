using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;

namespace NicheImageRipper.Core.FileDownloading;

internal static class YtDlpRunner
{
    private const string YoutubeCookiesFile = "yt_cookies.txt";

    public static async Task<bool> RunYtDlp(FileLink link, string path, string startMessage, string endMessage,
        DownloadContext context, CancellationToken cancellationToken = default)
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

        context.Logger.Debug("yt-dlp {cmd}", string.Join(" ", cmd));
        var (exitCode, output, _) = await ProcessRunner.RunSubprocess("yt-dlp", cmd, true,
            startMessage: startMessage, endMessage: endMessage, cancellationToken: cancellationToken);

        if (exitCode == 0 || link.LinkInfo != LinkInfo.YoutubeVideo)
        {
            return exitCode == 0;
        }

        var lines = output!.Split("\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var ageRestricted = lines.Any(line => line.Contains("This video is age-restricted"));
        if (ageRestricted)
        {
            if (!File.Exists(YoutubeCookiesFile))
            {
                context.Logger.Error("Video is age-restricted but no cookies file found at {YoutubeCookiesFile}",
                    YoutubeCookiesFile);
                return false;
            }

            context.Logger.Information("Video is age-restricted, trying again with cookies");
            cmd =
            [
                "--force-overwrites", "--cookies", $"\"{YoutubeCookiesFile}\"",
                "-P", $"\"{parent}\"", "-o", $"\"{filename}\"", $"\"{url}\"",
            ];

            (exitCode, _, _) = await ProcessRunner.RunSubprocess("yt-dlp", cmd, true,
                startMessage: startMessage, endMessage: endMessage, cancellationToken: cancellationToken);
        }

        context.Logger.Error("Failed to run yt-dlp: {ExitCode}", exitCode);
        return exitCode == 0;
    }
}