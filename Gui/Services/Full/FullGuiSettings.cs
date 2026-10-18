using NicheImageRipper.Gui.Models;

namespace NicheImageRipper.Gui.Services.Full;

public class FullGuiSettings : IGuiSettings
{
    private static GuiConfig Config => (GuiConfig) Sdk.Configuration.Config.Instance;
    
    public double NameWidth
    {
        get => Config.HistoryColumnWidths.NameWidth;
        set => Config.HistoryColumnWidths.NameWidth = value;
    }

    public double UrlWidth
    {
        get => Config.HistoryColumnWidths.UrlWidth;
        set => Config.HistoryColumnWidths.UrlWidth = value;
    }

    public double DateWidth
    {
        get => Config.HistoryColumnWidths.DateWidth;
        set => Config.HistoryColumnWidths.DateWidth = value;
    }

    public double CountWidth
    {
        get => Config.HistoryColumnWidths.CountWidth;
        set => Config.HistoryColumnWidths.CountWidth = value;
    }

    // Saved with the rest of GuiConfig when the ripper saves its config

    public bool ShowLogTimestampAndLevel
    {
        get => Config.LogDisplay.ShowTimestampAndLevel;
        set => Config.LogDisplay.ShowTimestampAndLevel = value;
    }

    public bool ShowDebugLogs
    {
        get => Config.LogDisplay.ShowDebug;
        set => Config.LogDisplay.ShowDebug = value;
    }

    public bool WrapLogLines
    {
        get => Config.LogDisplay.WrapLines;
        set => Config.LogDisplay.WrapLines = value;
    }

    public string Theme
    {
        get => Config.Theme;
        set => Config.Theme = value;
    }

    public string AccentColor
    {
        get => Config.AccentColor;
        set => Config.AccentColor = value;
    }
}