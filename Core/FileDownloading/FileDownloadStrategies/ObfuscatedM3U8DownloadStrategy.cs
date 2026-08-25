using System.Net;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using Sdk.DataStructures;
using Sdk.Exceptions;
using Sdk.FileDownloading;

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
            // Site-specific check left inline (exception-driven, not HTTP-status-driven — doesn't fit
            // IDownloadErrorHandler's shape) — consistent with pinning further site-quirk extraction for now.
            if (context.SiteName == "pornhub" && e.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
            {
                throw new UrlExpiredException(context.SiteName);
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