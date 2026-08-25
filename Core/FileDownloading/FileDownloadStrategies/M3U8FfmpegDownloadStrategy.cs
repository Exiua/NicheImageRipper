using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using Sdk.DataStructures;
using Sdk.FileDownloading;

namespace NicheImageRipper.Core.FileDownloading.FileDownloadStrategies;

using static ConfigAccess;

public sealed class M3U8FfmpegDownloadStrategy : IFileDownloadStrategy
{
    private readonly ObfuscatedM3U8DownloadStrategy _obfuscatedFallback = new();

    public IEnumerable<LinkInfo> HandlesLinkInfo => [LinkInfo.M3U8Ffmpeg];

    public async Task<DownloadResult> DownloadAsync(FileLink link, string imagePath, DownloadContext context,
                                                    CancellationToken cancellationToken = default)
    {
        var result = await DownloadM3U8ToMp4(imagePath, link, context, cancellationToken);
        return result.Outcome == DownloadOutcome.Success
            ? result
            : await _obfuscatedFallback.DownloadAsync(link, imagePath, context, cancellationToken);
    }

    private static async Task<DownloadResult> DownloadM3U8ToMp4(string filePath, FileLink link, DownloadContext context,
                                                                CancellationToken cancellationToken)
    {
        var url = link.Url;
        var referer = link.Referer;
        if (!filePath.Contains('.'))
        {
            if (url.Contains(".mp4"))
            {
                filePath += ".mp4";
            }
            else if (url.Contains(".webm"))
            {
                filePath += ".webm";
            }
            else
            {
                filePath += ".ts";
            }
        }

        string[] cmd = !string.IsNullOrEmpty(referer)
            ?
            [
                "-headers", $"\"Referer: {referer}\"",
                "-headers", $"\"User-Agent: {Config.UserAgent}\"",
                "-protocol_whitelist", "file,http,https,tcp,tls,crypto",
                "-i", $"\"{url}\"", "-c", "copy", $"\"{filePath}\""
            ]
            :
            [
                "-protocol_whitelist", "file,http,https,tcp,tls,crypto",
                "-i", $"\"{url}\"", "-c", "copy", $"\"{filePath}\""
            ];

        var result = await ProcessRunner.RunFfmpeg(cmd, cancellationToken: cancellationToken);
        context.Logger.Debug("Ffmpeg result: {Result}", result.GetShortErrorMessage());
        return result.IsSuccess() ? DownloadResult.Success() : DownloadResult.Failed(result.GetShortErrorMessage());
    }
}