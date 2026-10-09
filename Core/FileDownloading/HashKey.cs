namespace NicheImageRipper.Core.FileDownloading;

public class HashKey(byte[] hash)
{
    internal byte[] Hash { get; } = hash;
    
    public static implicit operator HashKey(byte[] hash)
    {
        return new HashKey(hash);
    }

    public static bool operator ==(HashKey a, HashKey b)
    {
        if (a.Hash.Length != b.Hash.Length)
        {
            return false;
        }

        return !a.Hash.Where((t, i) => t != b.Hash[i]).Any();
    }

    public static bool operator !=(HashKey a, HashKey b)
    {
        return !(a == b);
    }
    
    public override bool Equals(object? obj)
    {
        return obj is HashKey key && this == key;
    }
    
    public override int GetHashCode()
    {
        unchecked
        {
            return Hash.Aggregate(0, (current, b) => (current * 31) ^ b);
        }
    }
}