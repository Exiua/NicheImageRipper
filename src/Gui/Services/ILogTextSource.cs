using NicheImageRipper.Gui.Models;

namespace NicheImageRipper.Gui.Services;

public interface ILogTextSource
{
    /// <summary>
    ///     Incremented whenever the rendered text would change (new entry, clear, or display option change), so the
    ///     view can poll cheaply and only re-render when needed instead of once per log line.
    /// </summary>
    long Version { get; }

    /// <summary>Prefix each line with its time and level (the log file always has these).</summary>
    bool ShowTimestampAndLevel { get; set; }

    bool ShowDebug { get; set; }

    /// <summary>Renders the most recent entries using the current display options.</summary>
    string GetText();

    void Append(LogEntryModel entry);
    void Clear();
}
