using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Threading;

namespace NicheImageRipper.Gui.ExtensionMethods;

public static class GeneralExtensionMethods
{
    /// <summary>
    ///     Replace the contents of the collcetion with the provided contents
    /// </summary>
    /// <param name="collection">Collection to replace</param>
    /// <param name="source">Contents to replace with</param>
    /// <typeparam name="T">Type of the collection</typeparam>
    public static void Update<T>(this ObservableCollection<T> collection, IEnumerable<T> source)
    {
        Dispatcher.UIThread.Post(() =>
        {
            collection.Clear();
            foreach (var item in source)
            {
                collection.Add(item);
            }
        });
    }
}