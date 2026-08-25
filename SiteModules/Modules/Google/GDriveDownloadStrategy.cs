using Google.Apis.Drive.v3;
using Google.Apis.Services;



using Sdk.DataStructures;

namespace NicheImageRipper.SiteModules.Modules.Google;

public sealed class GDriveDownloadStrategy : IFileDownloadStrategy
{
    public IEnumerable<LinkInfo> HandlesLinkInfo => [GDriveLinkInfo.GDrive];

    public async Task<DownloadResult> DownloadAsync(FileLink link, string imagePath, DownloadContext context,
                                                    CancellationToken cancellationToken = default)
    {
        var parent = Directory.GetParent(imagePath)!.FullName;
        Directory.CreateDirectory(parent);
        var credentials = await TokenManager.GDriveAuthenticate();
        var service = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credentials,
            ApplicationName = "ImageRipper"
        });
        var request = service.Files.Get(link.Url);
        await using var stream = new FileStream(imagePath, FileMode.Create, FileAccess.Write);
        await request.DownloadAsync(stream, cancellationToken);
        return DownloadResult.Success();
    }
}