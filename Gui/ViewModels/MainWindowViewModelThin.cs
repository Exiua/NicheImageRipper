using System;
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
    
    public override string Title => $"GuiThin v{Version} - Core v{RipperClient.GetCoreVersion().Result}";

    public override void Initialize()
    {
        base.Initialize();
        Active = false;
    }
}