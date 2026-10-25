using System.Net;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Exceptions;
using NicheImageRipper.Sdk.FileDownloading;

namespace NicheImageRipper.Core.FileDownloading.FileDownloadStrategies;

using static ConfigAccess;

public sealed class ObfuscatedM3U8DownloadStrategy : IFileDownloadStrategy
{
    public IEnumerable<LinkInfo> HandlesLinkInfo => [LinkInfo.ObfuscatedM3U8];

    public async Task<DownloadResult> DownloadAsync(FileLink link, string imagePath, DownloadContext context,
                                                    CancellationToken cancellationToken = default)
    {
        try
        {
            var parent = Directory.GetParent(imagePath)!.FullName;
            var referer = link.Referer == "" ? null : link.Referer;
            var ext = Path.GetExtension(imagePath);
            if (ext == "")
            {
                link.Filename += ".mp4";
            }

            await M3U8Downloader.DownloadM3U8(link.Url, parent, link.Filename, referer,
                cancellationToken: cancellationToken);
            return DownloadResult.Success();
        }
        catch (HttpRequestException e)
        {
            // Sites can react to their own failures (e.g. expired stream links) through exception handlers
            var handler = context.ExceptionHandlers.FirstOrDefault(h => h.AppliesTo(e, link, context));
            if (handler is not null)
            {
                return handler.Handle(e, link, context);
            }

            context.Logger.Error(e, "Failed to download obfuscated M3U8");
            return DownloadResult.Failed("Failed to download obfuscated M3U8");
        }
        catch (Exception e)
        {
            context.Logger.Error(e, "Failed to download obfuscated M3U8");
            return DownloadResult.Failed("Failed to download obfuscated M3U8");
        }
    }
}