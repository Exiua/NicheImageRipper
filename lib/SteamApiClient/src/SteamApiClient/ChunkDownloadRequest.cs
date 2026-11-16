using Microsoft.Win32.SafeHandles;
using SteamKit2;

namespace SteamApiClient;

internal sealed record ChunkDownloadRequest(
    DepotManifest.ChunkData Chunk,
    SafeFileHandle File
);
