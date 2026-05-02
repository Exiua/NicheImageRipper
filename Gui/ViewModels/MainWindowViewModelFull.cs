using System;
using Gui.Services;
using Microsoft.Extensions.Logging;

namespace Gui.ViewModels;

public class MainWindowViewModelFull(
    ILogger<MainWindowViewModelFull> logger,
    IRipperClient ripperClient,
    IRipperSettings ripperSettings,
    IGuiSettings guiSettings,
    ILogTextSource logTextSource) 
    : MainWindowViewModelBase(ripperClient, ripperSettings, guiSettings, logTextSource, logger)
{
    private static readonly Version Version = new(2, 0, 0);

    public override string Title => $"Gui v{Version} - Core v{RipperClient.GetCoreVersion().Result}";
    public override bool IsThinClient => false;
}