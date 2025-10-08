namespace Core.DataStructures;

public class DownloadStats
{
    public List<string> FailedDownloads { get; set; } = [];
    public int ArchivesExtracted { get; set; }
    public int ArchivesExtractionFailed { get; set; }
    public int NumDuplicates { get; set; }
    
    public int FailedDownloadsCount => FailedDownloads.Count;
    
    public string GetStats(int total)
    {
        var success = total - FailedDownloadsCount - NumDuplicates;
        return $"""
                Results:
                    Total: {total}
                    Unique Downloads: {success}
                    Duplicates: {NumDuplicates}
                	Failed Downloads: {FailedDownloadsCount}
                	Archives Extracted: {ArchivesExtracted}
                	Failed Extractions: {ArchivesExtractionFailed}
                """;
    }
}