using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.FileDownloading;
using NicheImageRipper.Core.FileDownloading.FileDownloadStrategies;

namespace NicheImageRipper.SiteModules.Modules.Deviantart;

public sealed class DeviantArtExternalToolDownloadStrategy : IExternalToolDownloadStrategy
{
    public bool AppliesTo(string siteName) => siteName == "deviantart";

    public async Task<DownloadResult> DownloadAsync(RipInfo folderInfo, string fullPath, DownloadContext context,
                                                    CancellationToken cancellationToken)
    {
        var url = folderInfo.Urls[0].Url;
        var login = ConfigAccess.Config.Logins.GetValueOrDefault("deviantart");
        var cmd = new[]
        {
            "-D", $"\"{fullPath}\"", "-u", login.Username, "-p",
            login.Password, "--write-log", "log.txt", url
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