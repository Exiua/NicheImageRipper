using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Gui.Models;

namespace Gui.Services;

public sealed class RollingLogTextSource : ILogTextSource
{
    private readonly Queue<string> _entries = new();
    private readonly Lock _lock = new();

    public int MaxEntries { get; }

    public string CurrentText { get; private set; } = "";

    public event Action<string>? LogTextChanged;

    public RollingLogTextSource(int maxEntries = 500)
    {
        if (maxEntries <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxEntries));
        }

        MaxEntries = maxEntries;
    }

    public void Append(LogEntryModel entry)
    {
        var rendered = Render(entry);

        lock (_lock)
        {
            _entries.Enqueue(rendered);

            while (_entries.Count > MaxEntries)
            {
                _entries.Dequeue();
            }

            CurrentText = string.Join(Environment.NewLine, _entries);
        }

        LogTextChanged?.Invoke(CurrentText);
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
            CurrentText = "";
        }

        LogTextChanged?.Invoke(CurrentText);
    }

    private static string Render(LogEntryModel entry)
    {
        var sb = new StringBuilder();

        sb.Append('[');
        sb.Append(entry.Timestamp.ToString());
        sb.Append("] ");

        if (!string.IsNullOrWhiteSpace(entry.Level))
        {
            sb.Append(entry.Level);
            sb.Append(": ");
        }

        sb.Append(entry.RenderedMessage);

        if (!string.IsNullOrWhiteSpace(entry.Exception))
        {
            sb.AppendLine();
            sb.Append(entry.Exception);
        }

        return sb.ToString();
    }
}