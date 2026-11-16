namespace SteamApiClient;

internal sealed record DepotDownloadTarget(
    uint AppId,
    uint DepotId,
    ulong ManifestId,
    byte[] DepotKey,
    CdnServerPool ServerPool
);
