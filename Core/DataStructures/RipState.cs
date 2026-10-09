namespace NicheImageRipper.Core.DataStructures;

public class RipState
{
    public DownloadStats DownloadStats { get; set; } = null!;
    public SerializableIndexedHashes FilesHashes { get; set; } = null!;
}