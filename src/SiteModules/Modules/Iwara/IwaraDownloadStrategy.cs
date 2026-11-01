using IwaraApiClient.Models;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.FileDownloading;
using NicheImageRipper.Sdk.SiteParsing;

namespace NicheImageRipper.SiteModules.Modules.Iwara;

public sealed class IwaraDownloadStrategy : IFileDownloadStrategy
{
    public IEnumerable<LinkInfo> HandlesLinkInfo => [IwaraLinkInfo.Iwara];

    public async Task<DownloadResult> DownloadAsync(FileLink link, string imagePath, DownloadContext context,
                                                    CancellationToken cancellationToken = default)
    {
        var client = IwaraParser.Client;
        context.Logger.Debug("Downloading {Url} as {Filename}", link.Url, link.Filename);
        var videoId = link.Url.Split('/')[^1];
        // A full disk propagates as an IOException; the ripper recognises it and stops the queue
        var result = await client.DownloadVideo(videoId, imagePath, cancellationToken);

        await HtmlParser.JitterSleep(min: 250, max: 500, cancellationToken: cancellationToken);

        switch (result)
        {
            case RequestResult.VideoPrivate:
                context.Logger.Warning(
                    "Video is private. Please use the credentials of an account that has access to this video");
                return DownloadResult.SkipNotAFailure("Video is private");
            case RequestResult.VideoNotFound:
                context.Logger.Warning("Video not found. Please check the URL");
                return DownloadResult.SkipNotAFailure("Video not found", FailureKind.NotFound);
            case RequestResult.Success:
            case RequestResult.LoginFailure:
            case RequestResult.JsonNull:
            case RequestResult.FileUrlIsNull:
            case RequestResult.ExpirationIsMissingFromUrl:
            case RequestResult.SourceMetadataIsNull:
            case RequestResult.FailedToDownload:
            case RequestResult.RequestVideoFailed:
            default:
                return result == RequestResult.Success ? DownloadResult.Success() : DownloadResult.Failed();
        }
    }
}