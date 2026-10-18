using Google.Apis.Download;
using GoogleDownloadStatus = Google.Apis.Download.DownloadStatus;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.FileDownloading;

namespace NicheImageRipper.SiteModules.Modules.Google;

public sealed class GDriveDownloadStrategy : IFileDownloadStrategy
{
    public IEnumerable<LinkInfo> HandlesLinkInfo => [GDriveLinkInfo.GDrive];

    public async Task<DownloadResult> DownloadAsync(FileLink link, string imagePath, DownloadContext context,
                                                    CancellationToken cancellationToken = default)
    {
        var parent = Directory.GetParent(imagePath)!.FullName;
        Directory.CreateDirectory(parent);
        var service = await GDriveHelper.AuthenticateGDrive(cancellationToken);
        var request = service.Files.Get(link.Url);

        // Download next to the target and move it into place only on success, so a failure never leaves an empty
        // or partial file that looks like a finished download
        var tempPath = imagePath + ".part";
        try
        {
            IDownloadProgress progress;
            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
            {
                progress = await request.DownloadAsync(stream, cancellationToken);
            }

            if (progress.Status != GoogleDownloadStatus.Completed)
            {
                context.Logger.Error(progress.Exception, "Google Drive download failed: {FileId}", link.Url);
                return DownloadResult.Failed(progress.Exception?.Message ?? $"Download status: {progress.Status}");
            }

            File.Move(tempPath, imagePath, overwrite: true);
            return DownloadResult.Success();
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}