namespace Core.DataStructures;

public class DownloadStats
{
    public int FailedDownloads { get; set; }
    public int ArchivesExtracted { get; set; }
    public int NumDuplicates { get; set; }
    
    public string GetStats(int total)
    {
        var success = total - FailedDownloads - NumDuplicates;
        return $"""
                Results:
                    Total: {total}
                    Unique Downloads: {success}
                    Duplicates: {NumDuplicates}
                	Failed Downloads: {FailedDownloads}
                	Archives Extracted: {ArchivesExtracted}
                """;
    }
}