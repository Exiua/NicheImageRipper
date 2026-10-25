using System;
using NicheImageRipper.Gui.Models;
using NicheImageRipper.Gui.Utility;

namespace NicheImageRipper.Gui.Services.Full;

public class LocalGuiLogBridgeCoordinator : IGuiLogBridgeCoordinator
{
    private readonly ILogTextSource _logTextSource;

    public LocalGuiLogBridgeCoordinator(ILogTextSource logTextSource)
    {
        _logTextSource = logTextSource;

        foreach (var entry in GuiLogBridge.DrainPending())
        {
            _logTextSource.Append(entry);
        }

        GuiLogBridge.LogReceived += OnLogReceived;
    }

    private void OnLogReceived(LogEntryModel entry)
    {
        _logTextSource.Append(entry);
    }

    public void Dispose()
    {
        GuiLogBridge.LogReceived -= OnLogReceived;
        GC.SuppressFinalize(this);
    }
}