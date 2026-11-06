using MegaSdk;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.FileDownloading;

namespace NicheImageRipper.SiteModules.Modules.Mega;

/// <summary>
///     Downloads Mega links with the MEGA SDK. Links are normally single files (see <see cref="MegaLinkRule" />); a
///     whole-folder link (e.g. from an older saved rip, or one that couldn't be expanded) is downloaded into
///     <c>imagePath</c> as a directory.
/// </summary>
public sealed class MegaDownloadStrategy : IFileDownloadStrategy
{
    private const int MaxAttempts = 4;

    public IEnumerable<LinkInfo> HandlesLinkInfo => [MegaLinkInfo.Mega];

    public async Task<DownloadResult> DownloadAsync(FileLink link, string imagePath, DownloadContext context,
                                                    CancellationToken cancellationToken = default)
    {
        MegaClientProvider.EnsureAvailable();

        if (!MegaLink.TryParse(link.Url, out var megaLink))
        {
            return DownloadResult.Failed($"Unrecognized Mega link: {link.Url}", FailureKind.Unsupported);
        }

        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await DownloadOnceAsync(megaLink, imagePath, context, cancellationToken);
                return DownloadResult.Success();
            }
            catch (MegaException e) when (IsTransient(e.ErrorCode) && attempt < MaxAttempts)
            {
                var delay = TimeSpan.FromSeconds(5 * Math.Pow(2, attempt - 1));
                context.Logger.Warning("Mega download failed ({Error}), retrying in {Delay}s: {Url}", e.Message,
                    delay.TotalSeconds, link.Url);
                await Task.Delay(delay, cancellationToken);
            }
            catch (MegaException e)
            {
                context.Logger.Error("Mega download failed ({ErrorCode}: {Error}): {Url}", e.ErrorCode, e.Message,
                    link.Url);
                return ToResult(e);
            }
        }
    }

    private static async Task DownloadOnceAsync(MegaLink link, string imagePath, DownloadContext context,
                                                CancellationToken cancellationToken)
    {
        var client = await MegaClientProvider.GetSharedClientAsync(cancellationToken);
        using var node = link.Kind == MegaLinkKind.File
            ? await client.GetPublicNodeAsync(link.BaseUrl, cancellationToken)
            : await MegaHelper.AuthorizeAsync(link, cancellationToken)
              ?? throw new MegaException(MegaErrorCode.NotFound, "File or folder no longer exists in the Mega folder");

        string destination;
        if (node.IsFolder)
        {
            context.Logger.Debug("Downloading folder from Mega: {Link}", link);
            Directory.CreateDirectory(imagePath);
            // With a trailing separator the SDK creates the folder, under its MEGA name, inside imagePath
            destination = Path.TrimEndingDirectorySeparator(imagePath) + Path.DirectorySeparatorChar;
        }
        else
        {
            context.Logger.Debug("Downloading file from Mega: {Link}", link);
            Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
            destination = imagePath;
        }

        await client.DownloadAsync(node, destination, cancellationToken: cancellationToken);
    }

    private static bool IsTransient(MegaErrorCode errorCode)
    {
        return errorCode is MegaErrorCode.TryAgain or MegaErrorCode.RateLimit or MegaErrorCode.TemporarilyUnavailable
                         or MegaErrorCode.TooManyConnections or MegaErrorCode.Internal or MegaErrorCode.Incomplete
                         or MegaErrorCode.LocalNetwork or MegaErrorCode.LocalTimeout;
    }

    private static DownloadResult ToResult(MegaException e)
    {
        return e.ErrorCode switch
        {
            MegaErrorCode.NotFound or MegaErrorCode.Expired =>
                DownloadResult.NotFound($"File no longer exists on Mega: {e.Message}"),
            // The SDK reports taken-down files/accounts as TooMany or Blocked
            MegaErrorCode.TooMany or MegaErrorCode.Blocked =>
                DownloadResult.NotFound($"File or its account was taken down on Mega: {e.Message}"),
            MegaErrorCode.Key or MegaErrorCode.BadArguments =>
                DownloadResult.Failed($"Invalid Mega link or decryption key: {e.Message}", FailureKind.Unsupported),
            MegaErrorCode.OverQuota or MegaErrorCode.GoingOverQuota or MegaErrorCode.Paywall =>
                DownloadResult.Failed($"Mega transfer quota exceeded: {e.Message}"),
            MegaErrorCode.LocalNoSpace => DownloadResult.Failed($"Not enough disk space: {e.Message}"),
            _ => DownloadResult.Failed($"Mega download failed: {e.Message}"),
        };
    }
}
