using NicheImageRipper.Sdk.Common.ExtensionMethods;
using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.FileDownloading;
using NicheImageRipper.Sdk.Utility;

namespace NicheImageRipper.SiteModules.Modules.Deviantart;

public sealed class DeviantArtExternalToolDownloadStrategy : IExternalToolDownloadStrategy
{
    private static GeneralConfig Config => Sdk.Configuration.Config.Instance;
    
    public bool AppliesTo(string siteName) => siteName == "deviantart";

    public async Task<DownloadResult> DownloadAsync(RipInfo folderInfo, string fullPath, DownloadContext context,
                                                    CancellationToken cancellationToken)
    {
        var url = folderInfo.Urls[0].Url;
        var credentials = Config.GetSiteConfig(DeviantartParser.ParserName)?.Login;
        var (username, password) = credentials.Deconstruct();
        if (username.IsNullOrEmpty() || password.IsNullOrEmpty())
        {
            throw new InvalidOperationException("DeviantArt login credentials are not set in the configuration.");
        }
        
        var cmd = new[]
        {
            "-D", $"\"{fullPath}\"", "-u", username, "-p",
            password, "--write-log", "log.txt", url
        };
        var (exitCode, _, error) = await ProcessRunner.RunSubprocess("gallery-dl", cmd, captureError: true,
            startMessage: "Starting Deviantart download", endMessage: "Deviantart download finished",
            cancellationToken: cancellationToken);

        if (exitCode == 0)
        {
            return DownloadResult.Success();
        }

        // gallery-dl reports what went wrong on stderr; its last message is usually the useful one
        var lastError = error?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                              .LastOrDefault(line => line != "null");
        return DownloadResult.Failed(lastError is null
            ? $"gallery-dl exited with code {exitCode}"
            : $"gallery-dl exited with code {exitCode}: {lastError}");
    }
}