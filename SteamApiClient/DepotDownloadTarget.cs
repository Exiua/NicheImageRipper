namespace SteamApiClient;

internal sealed record DepotDownloadTarget(
    uint AppId,
    ulong ManifestId,
    byte[] DepotKey,
    CdnServerPool ServerPool
);