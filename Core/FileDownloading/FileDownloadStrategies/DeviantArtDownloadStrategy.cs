using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;

namespace NicheImageRipper.Core.FileDownloading.FileDownloadStrategies;

using static ConfigAccess;

public sealed class DeviantArtDownloadStrategy : IFileDownloadStrategy
{
    public IEnumerable<LinkInfo> HandlesLinkInfo => [LinkInfo.DeviantArt];
    public bool SupportsPostProcessing => false; // whole-gallery download via external tool, no single verifiable file

    public async Task<DownloadResult> DownloadAsync(FileLink link, string imagePath, DownloadContext context,
                                                    CancellationToken cancellationToken = default)
    {
        // TODO: Add feature check and throw if not present
        var cmd = new[]
        {
            "-D", $"\"{imagePath}\"", "-u", Config.Logins.DeviantArt.Username, "-p",
            Config.Logins.DeviantArt.Password, "--write-log", "log.txt", link.Url
        };
        var (exitCode, _, _) = await ProcessRunner.RunSubprocess("gallery-dl", cmd,
            startMessage: "Starting Deviantart download", endMessage: "Deviantart download finished",
            cancellationToken: cancellationToken);

        if (exitCode != 0)
        {
            context.Logger.Error("Failed to download from DeviantArt");
        }

        return exitCode == 0 ? DownloadResult.Success() : DownloadResult.Failed();
    }
}