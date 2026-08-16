using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.Configuration;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.FileDownloading;
using NicheImageRipper.Core.FileDownloading.FileDownloadStrategies;

namespace NicheImageRipper.SiteModules.Modules.Mega;

public sealed class MegaDownloadStrategy : IFileDownloadStrategy
{
    private static GeneralConfig Config => Core.Configuration.Config.Instance;
    
    public IEnumerable<LinkInfo> HandlesLinkInfo => [ MegaLinkInfo.Mega ];
    public bool SupportsPostProcessing => false;

    public async Task<DownloadResult> DownloadAsync(FileLink link, string imagePath, DownloadContext context,
                                                     CancellationToken cancellationToken = default)
    {
        if (!Core.NicheImageRipper.AvailableFeatures.HasFlag(ExternalFeatureSupport.MegaCmd))
        {
            throw new FeatureNotAvailableException(ExternalFeatureSupport.MegaCmd);
        }

        context.Logger.Debug("Logging in to MegaCmd");
        var (email, password) = Config.Logins.GetValueOrDefault("mega").Deconstruct();
        if (email.IsNullOrWhiteSpace() || password.IsNullOrWhiteSpace())
        {
            throw new RipperException("MegaCmd login credentials are not set in the configuration");
        }
        
        if (!MegaSessionManager.EnsureLoggedIn(email, password))
        {
            var e = new RipperException("Unable to login to MegaCmd");
            context.Logger.Error(e, "Unable to login to MegaCmd");
            throw e;
        }

        string downloadPath;
        if (link.Url.Contains("/file/"))
        {
            context.Logger.Debug("Downloading file from Mega: {Url}", link.Url);
            downloadPath = Path.GetDirectoryName(imagePath)!;
        }
        else
        {
            context.Logger.Debug("Downloading folder from Mega: {Url}", link.Url);
            downloadPath = imagePath;
            Directory.CreateDirectory(downloadPath);
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(60));
            try
            {
                var success = await MegaApi.DownloadAsync(link.Url, downloadPath, cts.Token);
                return success ? DownloadResult.Success() : DownloadResult.Failed();
            }
            catch (OperationCanceledException)
            {
                context.Logger.Warning("Mega download timed out, retrying...");
            }
            catch (Exception e)
            {
                context.Logger.Error(e, "Failed to download from Mega: {Url}", link.Url);
                if (e.Message.Contains("No such file or directory"))
                {
                    context.Logger.Error("The specified file or directory does not exist on Mega: {Url}", link.Url);
                    return DownloadResult.Failed("The specified file or directory does not exist on Mega");
                }

                if (e.Message.Contains("Invalid URL"))
                {
                    context.Logger.Error("The provided URL is invalid: {Url}", link.Url);
                    return DownloadResult.Failed("The provided URL is invalid");
                }

                throw;
            }
        }
    }
}