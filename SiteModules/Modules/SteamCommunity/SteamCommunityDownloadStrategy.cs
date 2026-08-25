using System.Net.Sockets;
using Sdk.Common.ExtensionMethods;
using Sdk.Configuration;
using Sdk.DataStructures;
using Sdk.Exceptions;
using Sdk.FileDownloading;
using SteamKit2;

namespace NicheImageRipper.SiteModules.Modules.SteamCommunity;

public sealed class SteamCommunityDownloadStrategy : IFileDownloadStrategy
{
    private static GeneralConfig Config => Sdk.Configuration.Config.Instance;

    public IEnumerable<LinkInfo> HandlesLinkInfo => [SteamCommunityLinkInfo.SteamCommunity];
    public bool SupportsPostProcessing => false;

    public async Task<DownloadResult> DownloadAsync(FileLink link, string imagePath, DownloadContext context,
                                                    CancellationToken cancellationToken = default)
    {
        var destinationFolder = Path.GetDirectoryName(imagePath)!;
        Directory.CreateDirectory(destinationFolder);
        var ids = link.Url.Split('/')[^1].Split('|');
        var fileId = ids[1];
        var (username, password) = Config.Logins.GetValueOrDefault(SteamCommunityParser.ParserName).Deconstruct();
        var client = SteamCommunityParser.SteamApiClient;
        if (username.IsNullOrEmpty() || password.IsNullOrEmpty())
        {
            throw new RipperException(
                "Steam credentials are not set. Please add your Steam username and password to the config file.");
        }

        try
        {
            await client.LoginAsync(username, password, cancellationToken);
            await client.DownloadWorkshopFileAsync(ulong.Parse(fileId), destinationFolder, cancellationToken);
        }
        catch (SteamKitWebRequestException e) when (e.Message.Contains("503"))
        {
            context.Logger.Warning(e, "Steam Community is currently unavailable (503), retrying...");
            await Task.Delay(2500, cancellationToken);
            return DownloadResult.Failed("Steam Community unavailable (503)");
        }
        catch (AsyncJobFailedException e)
        {
            context.Logger.Warning(e, "Failed to download Steam Community file, retrying...");
            await client.LogoutAsync(cancellationToken);
            return DownloadResult.Failed("AsyncJobFailedException");
        }
        catch (HttpRequestException e) when (e.InnerException is IOException { InnerException: SocketException } ex)
        {
            context.Logger.Warning(ex, "Network error while trying to download Steam Community: {Url}", link.Url);
            await client.LogoutAsync(cancellationToken);
            return DownloadResult.Failed("Network error");
        }
        catch (Exception e)
        {
            context.Logger.Error(e, "Failed to download Steam Community file");
            return DownloadResult.Failed("Failed to download Steam Community file");
        }

        return DownloadResult.Success();
    }
}