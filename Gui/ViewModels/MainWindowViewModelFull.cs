using System;
using System.Collections.Generic;
using Core;
using Core.DataStructures;
using Core.History;
using Gui.Services;

namespace Gui.ViewModels;

public class MainWindowViewModelFull(
    IRipperClient ripperClient,
    IRipperSettings ripperSettings,
    IGuiSettings guiSettings,
    ILogTextSource logTextSource) 
    : MainWindowViewModelBase(ripperClient, ripperSettings, guiSettings, logTextSource)
{
    private static readonly Version Version = new(1, 0, 0);

    public override string Title => $"Gui v{Version} - Core v{NicheImageRipper.Version}";

    protected override void ClearCache()
    {
        NicheImageRipper.ClearCache();
    }

    protected override List<HistoryEntry> GetHistoryPage(int start, int offset, HistoryFilter? filter = null) =>
        NicheImageRipper.GetHistoryPage(start, offset, filter);
}