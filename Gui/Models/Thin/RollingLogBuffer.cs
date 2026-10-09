using System;
using System.Collections.Generic;
using System.Threading;

namespace NicheImageRipper.Gui.Models.Thin;

public sealed class RollingLogBuffer
{
    private readonly Queue<string> _entries = new();
    private readonly Lock _lock = new();

    public int MaxEntries { get; }

    public RollingLogBuffer(int maxEntries)
    {
        if (maxEntries <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxEntries));
        }

        MaxEntries = maxEntries;
    }

    public void Add(string entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
        {
            return;
        }

        lock (_lock)
        {
            _entries.Enqueue(entry);

            while (_entries.Count > MaxEntries)
            {
                _entries.Dequeue();
            }
        }
    }

    public string GetCombinedText()
    {
        lock (_lock)
        {
            return string.Join(Environment.NewLine, _entries);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }
}