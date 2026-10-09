using SteamKit2;

namespace SteamApiClient;

internal sealed record ChunkDownloadRequest(
    DepotDownloadTarget Target,
    DepotManifest.ChunkData Chunk,
    FileStream FileStream,
    SemaphoreSlim FileLock
);