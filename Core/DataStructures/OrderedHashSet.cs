using Core.ExtensionMethods;

namespace Core.DataStructures;

public class OrderedHashSet<T>
{
    private readonly List<T> _list = [];
    private readonly HashSet<T> _set = [];
    
    public int Count => _list.Count;
    
    public bool Add(T item)
    {
        if (!_set.Add(item))
        {
            return false;
        }

        _list.Add(item);
        return true;

    }
    
    public IEnumerable<(int i, T)> Enumerate()
    {
        return _list.Enumerate();
    }
}