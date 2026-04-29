using System;
using Gui.Models;
using Gui.Utility;

namespace Gui.Services.Thin;

public class RemoteGuiLogBridgeCoordinator: IGuiLogBridgeCoordinator
{
    private readonly ILogTextSource _logTextSource;

    public RemoteGuiLogBridgeCoordinator(ILogTextSource logTextSource)
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