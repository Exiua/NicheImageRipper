using System;
using System.Collections.Generic;
using System.Threading;
using NicheImageRipper.Gui.Models;

namespace NicheImageRipper.Gui.Services.Shared;

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
        if (entry.Message?.Contains(
                "[Visual Avalonia.Win32.WinRT.Composition.WinUiCompositorConnection+RunLoopHandler]") == true)
        {
            return;
        }

        if (entry.Level is null or "Debug")
        {
            return;
        }
        
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
        var time = entry.Timestamp?.ToLocalTime().ToString("HH:mm:ss");
        var level = ToShortLevel(entry.Level ?? "Information");

        //var line = $"[{time} {level}] {entry.RenderedMessage}";
        var line = $"{entry.Message}";

        if (!string.IsNullOrWhiteSpace(entry.Exception))
        {
            line += Environment.NewLine + entry.Exception;
        }

        return line;
    }
    
    private static string ToShortLevel(string level)
    {
        return level switch
        {
            "Verbose" => "VRB",
            "Debug" => "DBG",
            "Information" => "INF",
            "Warning" => "WRN",
            "Error" => "ERR",
            "Fatal" => "FTL",
            _ => level.Length >= 3 ? level[..3].ToUpperInvariant() : level.ToUpperInvariant()
        };
    }
}