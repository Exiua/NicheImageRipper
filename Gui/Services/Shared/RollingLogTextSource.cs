using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NicheImageRipper.Gui.Models;

namespace NicheImageRipper.Gui.Services.Shared;

public sealed class RollingLogTextSource : ILogTextSource
{
    private readonly Queue<LogEntryModel> _entries = new();
    private readonly Lock _lock = new();

    private long _version;
    private long _renderedVersion = -1;
    private string _renderedText = "";

    /// <summary>Entries kept in memory, including Debug ones so they can be shown after toggling them on.</summary>
    public int MaxStoredEntries { get; }

    /// <summary>Most recent visible entries rendered into the log tab.</summary>
    public int MaxDisplayedEntries { get; }

    public long Version => Interlocked.Read(ref _version);

    public bool ShowTimestampAndLevel
    {
        get;
        set => SetOption(ref field, value);
    }

    public bool ShowDebug
    {
        get;
        set => SetOption(ref field, value);
    }

    public RollingLogTextSource() : this(2000, 500)
    {
    }

    public RollingLogTextSource(int maxStoredEntries, int maxDisplayedEntries)
    {
        if (maxStoredEntries <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxStoredEntries));
        }

        if (maxDisplayedEntries <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDisplayedEntries));
        }

        MaxStoredEntries = maxStoredEntries;
        MaxDisplayedEntries = maxDisplayedEntries;
    }

    public void Append(LogEntryModel entry)
    {
        if (entry.Message?.Contains(
                "[Visual Avalonia.Win32.WinRT.Composition.WinUiCompositorConnection+RunLoopHandler]") == true)
        {
            return;
        }

        if (entry.Level is null)
        {
            return;
        }

        lock (_lock)
        {
            _entries.Enqueue(entry);
            while (_entries.Count > MaxStoredEntries)
            {
                _entries.Dequeue();
            }
        }

        Interlocked.Increment(ref _version);
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }

        Interlocked.Increment(ref _version);
    }

    public string GetText()
    {
        lock (_lock)
        {
            var version = Version;
            if (version == _renderedVersion)
            {
                return _renderedText;
            }

            var visible = _entries.Where(e => ShowDebug || e.Level != "Debug").ToList();
            var builder = new StringBuilder();
            foreach (var entry in visible.Skip(Math.Max(0, visible.Count - MaxDisplayedEntries)))
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                Render(builder, entry);
            }

            _renderedText = builder.ToString();
            _renderedVersion = version;
            return _renderedText;
        }
    }

    private void SetOption(ref bool field, bool value)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        Interlocked.Increment(ref _version);
    }

    private void Render(StringBuilder builder, LogEntryModel entry)
    {
        if (ShowTimestampAndLevel)
        {
            var time = entry.Timestamp?.ToLocalTime().ToString("HH:mm:ss") ?? "--:--:--";
            builder.Append('[').Append(time).Append(' ').Append(ToShortLevel(entry.Level ?? "Information")).Append("] ");
        }

        builder.Append(entry.Message);

        if (!string.IsNullOrWhiteSpace(entry.Exception))
        {
            builder.AppendLine().Append(entry.Exception);
        }
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
