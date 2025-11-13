using Core.ExtensionMethods;
using Core.Managers;

namespace Core.DataStructures;

internal class OrderedHashSet<T>
{
    private readonly List<T> _list = [];
    private readonly HashSet<T> _set = [];
    
    internal int Count => _list.Count;
    
    internal bool Add(T item)
    {
        if (!_set.Add(item))
        {
            return false;
        }

        _list.Add(item);
        return true;

    }
    
    internal IEnumerable<(int i, T)> Enumerate()
    {
        return _list.Enumerate();
    }
}