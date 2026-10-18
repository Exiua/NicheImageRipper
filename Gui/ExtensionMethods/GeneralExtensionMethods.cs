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

    /// <summary>
    ///     Brings the collection in line with <paramref name="source"/> using the fewest removes, moves and inserts,
    ///     so bound controls keep their selection and scroll position (unlike <see cref="Update{T}"/>, which
    ///     clears and re-adds everything).
    /// </summary>
    public static void SyncTo<T>(this ObservableCollection<T> collection, IReadOnlyList<T> source)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var comparer = EqualityComparer<T>.Default;
            var wanted = new HashSet<T>(source, comparer);
            for (var i = collection.Count - 1; i >= 0; i--)
            {
                if (!wanted.Contains(collection[i]))
                {
                    collection.RemoveAt(i);
                }
            }

            for (var i = 0; i < source.Count; i++)
            {
                if (i < collection.Count && comparer.Equals(collection[i], source[i]))
                {
                    continue;
                }

                var existing = -1;
                for (var j = i + 1; j < collection.Count; j++)
                {
                    if (comparer.Equals(collection[j], source[i]))
                    {
                        existing = j;
                        break;
                    }
                }

                if (existing >= 0)
                {
                    collection.Move(existing, i);
                }
                else
                {
                    collection.Insert(i, source[i]);
                }
            }

            while (collection.Count > source.Count)
            {
                collection.RemoveAt(collection.Count - 1);
            }
        });
    }
}