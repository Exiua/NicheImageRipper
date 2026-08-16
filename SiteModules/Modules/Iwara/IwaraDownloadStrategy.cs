using IwaraApiClient.Models;
using NicheImageRipper.Common.Exceptions;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.FileDownloading;
using NicheImageRipper.Core.SiteParsing;

namespace NicheImageRipper.SiteModules.Modules.Iwara;

public sealed class IwaraDownloadStrategy : IFileDownloadStrategy
{
    public IEnumerable<LinkInfo> HandlesLinkInfo => [IwaraLinkInfo.Iwara];

    public async Task<DownloadResult> DownloadAsync(FileLink link, string imagePath, DownloadContext context,
                                                    CancellationToken cancellationToken = default)
    {
        var client = context.ClientManager.IwaraClient;
        context.Logger.Debug("Downloading {Url} as {Filename}", link.Url, link.Filename);
        var videoId = link.Url.Split('/')[^1];
        RequestResult result;
        try
        {
            result = await client.DownloadVideo(videoId, imagePath, cancellationToken);
        }
        catch (IOException e) when (e.Message.StartsWith("There is not enough space on the disk"))
        {
            throw new NotEnoughDiskSpaceException(e);
        }

        await HtmlParser.JitterSleep(min: 250, max: 500, cancellationToken: cancellationToken);

        switch (result)
        {
            case RequestResult.VideoPrivate:
                context.Logger.Warning(
                    "Video is private. Please use the credentials of an account that has access to this video");
                return DownloadResult.SkipNotAFailure("Video is private");
            case RequestResult.VideoNotFound:
            {
                context.Logger.Warning("Video not found. Please check the URL");
                var parentPath = Directory.GetParent(imagePath)!.FullName;
                await File.AppendAllTextAsync(Path.Combine(parentPath, "failed.txt"), link.Url + "\n",
                    cancellationToken);
                return DownloadResult.SkipNotAFailure("Video not found");
            }
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