using System;
using System.Collections.Generic;
using Core.DataStructures;
using Core.History;
using Gui.Services;

namespace Gui.ViewModels;

public class MainWindowViewModelThin(
    IRipperClient ripperClient,
    IRipperSettings ripperSettings,
    IGuiSettings guiSettings,
    ILogTextSource logTextSource)
    : MainWindowViewModelBase(ripperClient, ripperSettings, guiSettings, logTextSource)
{
    private static readonly Version Version = new(1, 0, 0);
    
    public override string Title => $"GuiThin v{Version} - Core v{"TODO"}";
    public override int HistoryCount { get; }


    protected override void ClearCache()
    {
        throw new NotImplementedException();
    }

    protected override List<HistoryEntry> GetHistoryPage(int start, int offset, HistoryFilter? filter = null)
    {
        throw new NotImplementedException();
    }
}