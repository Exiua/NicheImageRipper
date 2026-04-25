using System;
using Gui.Services;

namespace Gui.ViewModels;

public class MainWindowViewModelFull(
    IRipperClient ripperClient,
    IRipperSettings ripperSettings,
    IGuiSettings guiSettings,
    ILogTextSource logTextSource) 
    : MainWindowViewModelBase(ripperClient, ripperSettings, guiSettings, logTextSource)
{
    private static readonly Version Version = new(2, 0, 0);

    public override string Title => $"Gui v{Version} - Core v{RipperClient.GetCoreVersion().Result}";
}