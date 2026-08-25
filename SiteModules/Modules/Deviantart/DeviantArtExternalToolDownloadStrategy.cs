



using Sdk.Configuration;
using Sdk.DataStructures;

namespace NicheImageRipper.SiteModules.Modules.Deviantart;

public sealed class DeviantArtExternalToolDownloadStrategy : IExternalToolDownloadStrategy
{
    private static GeneralConfig Config => Sdk.Configuration.Config.Instance;
    
    public bool AppliesTo(string siteName) => siteName == "deviantart";

    public async Task<DownloadResult> DownloadAsync(RipInfo folderInfo, string fullPath, DownloadContext context,
                                                    CancellationToken cancellationToken)
    {
        var url = folderInfo.Urls[0].Url;
        var (username, password) = Config.Logins.GetValueOrDefault("deviantart").Deconstruct();
        if (username.IsNullOrEmpty() || password.IsNullOrEmpty())
        {
            throw new InvalidOperationException("DeviantArt login credentials are not set in the configuration.");
        }
        
        var cmd = new[]
        {
            "-D", $"\"{fullPath}\"", "-u", username, "-p",
            password, "--write-log", "log.txt", url
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