using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CoreGui.Utility;

public static class ExtensionMethods
{
    public static void Update<T>(this ObservableCollection<T> collection, List<T> source)
    {
        collection.Clear();
        foreach (var item in source)
        {
            collection.Add(item);
        }
    }
}