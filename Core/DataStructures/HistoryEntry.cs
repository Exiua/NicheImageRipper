using System.Text.Json.Serialization;

namespace Core.DataStructures;

public class HistoryEntry
{
    public string DirectoryName { get; } = null!;
    public string Url { get; } = null!;
    public DateTime Date { get; }
    public int NumUrls { get; }
    
    [JsonConstructor]
    public HistoryEntry()
    {
        
    }
    
    public HistoryEntry(string directoryName, string url, int numUrls)
    {
        DirectoryName = directoryName;
        Url = url;
        Date = DateTime.Now;
        NumUrls = numUrls;
    }
    
    public static bool operator ==(HistoryEntry left, HistoryEntry right)
    {
        return left.DirectoryName == right.DirectoryName;
    }

    public static bool operator !=(HistoryEntry left, HistoryEntry right)
    {
        return !(left == right);
    }

    private bool Equals(HistoryEntry other)
    {
        return DirectoryName == other.DirectoryName && NumUrls == other.NumUrls;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != GetType())
        {
            return false;
        }

        return Equals((HistoryEntry)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(DirectoryName, NumUrls);
    }
}