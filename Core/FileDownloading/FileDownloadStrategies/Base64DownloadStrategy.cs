using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;

namespace NicheImageRipper.Core.FileDownloading.FileDownloadStrategies;

public sealed class Base64DownloadStrategy : IFileDownloadStrategy
{
    public IEnumerable<LinkInfo> HandlesLinkInfo => [ LinkInfo.Base64 ];

    public async Task<DownloadResult> DownloadAsync(FileLink link, string imagePath, DownloadContext context,
                                                    CancellationToken cancellationToken = default)
    {
        try
        {
            var base64Data = link.Url.Split(',')[1];
            var imageData = Convert.FromBase64String(base64Data);
            await File.WriteAllBytesAsync(imagePath, imageData, cancellationToken);
            return DownloadResult.Success();
        }
        catch (FormatException e)
        {
            context.Logger.Error(e, "Failed to decode base64 image");
            return DownloadResult.Failed("Failed to decode base64 image");
        }
    }
}