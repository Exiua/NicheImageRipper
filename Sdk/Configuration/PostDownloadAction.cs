namespace NicheImageRipper.Sdk.Configuration;

public class PostDownloadAction
{
    public PostDownloadActionDirectoryLevel DirectoryLevel { get; set; } = new();
    public PostDownloadActionFileLevel FileLevel { get; set; } = new();
}