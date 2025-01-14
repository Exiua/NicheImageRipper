namespace Core.DataStructures;

public class DownloadStats
{
    public int FailedDownloads { get; set; }
    public int ArchivesExtracted { get; set; }
    
    public string GetStats()
    {
        return $"""
                Results:
                	Failed Downloads: {FailedDownloads}
                	Archives Extracted: {ArchivesExtracted}
                """;
    }
}