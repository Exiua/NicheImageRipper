namespace Core.DataStructures;

public class RipState
{
    public DownloadStats DownloadStats { get; set; } = null!;
    public List<string> FilesHashes { get; set; } = null!;
}