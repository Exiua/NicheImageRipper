using System.Collections.ObjectModel;

namespace Common.Gui.ExtensionMethods;

public static class GeneralExtensionMethods
{
    public static void Update<T>(this ObservableCollection<T> collection, IEnumerable<T> source)
    {
        collection.Clear();
        foreach (var item in source)
        {
            collection.Add(item);
        }
    }
}