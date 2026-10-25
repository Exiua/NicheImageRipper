using NicheImageRipper.Core.FileDownloading;

namespace NicheImageRipper.Core.DataStructures;

public class IndexedHashes
{
    private readonly HashSet<HashKey> _hashes;
    private readonly Dictionary<int, HashKey> _indexToHash;

    public IndexedHashes()
    {
        _hashes = [];
        _indexToHash = new Dictionary<int, HashKey>();
    }

    private IndexedHashes(HashSet<HashKey> hashes, Dictionary<int, HashKey> indexToHash)
    {
        _hashes = hashes;
        _indexToHash = indexToHash;
    }

    public SerializableIndexedHashes Serialize()
    {
        var hashes = _hashes.Select(hashKey => Convert.ToHexString(hashKey.Hash)).ToList();
        var indexToHash = _indexToHash.ToDictionary(kvp => kvp.Key, kvp => Convert.ToHexString(kvp.Value.Hash));
        return new SerializableIndexedHashes
        {
            Hashes = hashes,
            IndexToHash = indexToHash
        };
    }

    public static IndexedHashes Deserialize(SerializableIndexedHashes data)
    {
        var hashes = data.Hashes.Select(hashKey => new HashKey(Convert.FromHexString(hashKey))).ToHashSet();
        var indexToHash = data.IndexToHash.ToDictionary(kvp => kvp.Key, kvp => new HashKey(Convert.FromHexString(kvp.Value)));
        return new IndexedHashes(hashes, indexToHash);
    }
    
    public bool Add(HashKey hash, int index)
    {
        var added = _hashes.Add(hash);
        if (added)
        {
            _indexToHash[index] = hash;
        }
        
        return added;
    }
    
    public bool Contains(HashKey hash)
    {
        return _hashes.Contains(hash);
    }

    public void TruncateToIndex(int index)
    {
        var keysToRemove = _indexToHash.Keys.Where(i => i >= index).ToList();
        foreach (var key in keysToRemove)
        {
            if (_indexToHash.TryGetValue(key, out var hash))
            {
                _hashes.Remove(hash);
                _indexToHash.Remove(key);
            }
        }
    }
}

public class SerializableIndexedHashes
{
    public List<string> Hashes { get; set; } = [];
    public Dictionary<int, string> IndexToHash { get; set; } = new();
}