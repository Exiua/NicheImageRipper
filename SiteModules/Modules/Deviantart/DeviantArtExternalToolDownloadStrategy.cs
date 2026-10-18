using System.Text.Json;
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
        
        // Passed in a short-lived config file rather than -u/-p, which other local processes could read from
        // gallery-dl's command line
        var credentialsConfig = Path.Combine(Path.GetTempPath(), $"nir-gallery-dl-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(credentialsConfig, JsonSerializer.Serialize(new
        {
            extractor = new { deviantart = new { username, password } },
        }), cancellationToken);

        int exitCode;
        string? error;
        try
        {
            string[] cmd = ["--config", credentialsConfig, "-D", fullPath, "--write-log", AppData.EnsureDirectory("Logs") + Path.DirectorySeparatorChar + "gallery-dl.log", url];
            (exitCode, _, error) = await ProcessRunner.RunSubprocess("gallery-dl", cmd, captureError: true,
                startMessage: "Starting Deviantart download", endMessage: "Deviantart download finished",
                cancellationToken: cancellationToken);
        }
        finally
        {
            File.Delete(credentialsConfig);
        }

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