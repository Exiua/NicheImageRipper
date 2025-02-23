namespace Core.FileDownloading;

public class HashKey(byte[] hash)
{
    private readonly byte[] _hash = hash;
    
    public static implicit operator HashKey(byte[] hash)
    {
        return new HashKey(hash);
    }

    public static bool operator ==(HashKey a, HashKey b)
    {
        if (a._hash.Length != b._hash.Length)
        {
            return false;
        }

        return !a._hash.Where((t, i) => t != b._hash[i]).Any();
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
            return _hash.Aggregate(0, (current, b) => (current * 31) ^ b);
        }
    }
}