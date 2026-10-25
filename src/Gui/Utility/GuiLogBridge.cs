using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using NicheImageRipper.Gui.Models;

namespace NicheImageRipper.Gui.Utility;

public static class GuiLogBridge
{
    private static readonly ConcurrentQueue<LogEntryModel> PendingEntries = new();
    public static event Action<LogEntryModel>? LogReceived;

    public static void Publish(LogEntryModel entry)
    {
        PendingEntries.Enqueue(entry);
        LogReceived?.Invoke(entry);
    }

    public static List<LogEntryModel> DrainPending()
    {
        var entries = new List<LogEntryModel>();

        while (PendingEntries.TryDequeue(out var entry))
        {
            entries.Add(entry);
        }

        return entries;
    }
}