using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Exceptions;

namespace NicheImageRipper.Core.FileDownloading.PostDownloadValidators;

public sealed class EHentaiMinimumSizeValidator : IPostDownloadValidator
{
    private const long MinimumFileSize = 1024; // 1KB

    public bool AppliesTo(FileLink link, DownloadContext context) => context.SiteName == "e-hentai";

    public Task<DownloadResult> ValidateAsync(string filePath, FileLink link, DownloadContext context,
                                              CancellationToken cancellationToken)
    {
        var size = new FileInfo(filePath).Length;
        if (size >= MinimumFileSize)
        {
            return Task.FromResult(DownloadResult.Success());
        }

        context.Logger.Warning("Downloaded file is very small: {FilePath} ({Size} bytes)", filePath, size);
        throw new UrlExpiredException(context.SiteName);
    }
}