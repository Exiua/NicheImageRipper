namespace NicheImageRipper.Core.DataStructures;

public class DownloadStats
{
    public HashSet<string> FailedDownloads { get; set; } = [];
    public int ArchivesExtracted { get; set; }
    public int ArchivesExtractionFailed { get; set; }
    public int NumDuplicates { get; set; }
    
    public int FailedDownloadsCount => FailedDownloads.Count;
    public bool HasFailedDownloads => FailedDownloadsCount > 0;
    
    public string GetStats(int total)
    {
        var success = total - FailedDownloadsCount - NumDuplicates;
        var failed = "";
        if (FailedDownloadsCount > 0)
        {
            failed = "\nFailed Links:\n    " + string.Join("\n    ", FailedDownloads);
        }
        
        return $"""
                Results:
                    Total: {total}
                    Unique Downloads: {success}
                    Duplicates: {NumDuplicates}
                	Failed Downloads: {FailedDownloadsCount}
                	Archives Extracted: {ArchivesExtracted}
                	Failed Extractions: {ArchivesExtractionFailed}{failed}
                """;
    }
}