using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;

namespace NicheImageRipper.Core.FileDownloading.FileDownloadStrategies;

using static ConfigAccess;

public sealed class MegaDownloadStrategy : IFileDownloadStrategy
{
    public IEnumerable<LinkInfo> HandlesLinkInfo => [ LinkInfo.Mega ];
    public bool SupportsPostProcessing => false;

    public async Task<DownloadResult> DownloadAsync(FileLink link, string imagePath, DownloadContext context,
                                                     CancellationToken cancellationToken = default)
    {
        if (!NicheImageRipper.AvailableFeatures.HasFlag(ExternalFeatureSupport.MegaCmd))
        {
            throw new FeatureNotAvailableException(ExternalFeatureSupport.MegaCmd);
        }

        context.Logger.Debug("Logging in to MegaCmd");
        var (email, password) = Config.Logins.Mega;
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