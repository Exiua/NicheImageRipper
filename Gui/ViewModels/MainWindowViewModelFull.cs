using System;
using NicheImageRipper.Sdk.Utility;
using Microsoft.Extensions.Logging;
using NicheImageRipper.Gui.Services;

namespace NicheImageRipper.Gui.ViewModels;

public class MainWindowViewModelFull(
    ILogger<MainWindowViewModelFull> logger,
    IRipperClient ripperClient,
    IRipperSettings ripperSettings,
    IGuiSettings guiSettings,
    ILogTextSource logTextSource) 
    : MainWindowViewModelBase(ripperClient, ripperSettings, guiSettings, logTextSource, logger)
{
    private static readonly Version Version = AssemblyVersionUtility.Of<MainWindowViewModelBase>();

    public override string Title => $"Gui v{Version} - Core v{RipperClient.GetCoreVersion().Result}";
    public override bool IsThinClient => false;
}