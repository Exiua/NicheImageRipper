namespace Core.DataStructures;

internal class RipState
{
    internal DownloadStats DownloadStats { get; set; } = null!;
    internal List<string> FilesHashes { get; set; } = null!;
}