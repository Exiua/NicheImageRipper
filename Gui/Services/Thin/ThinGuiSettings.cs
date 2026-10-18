using NicheImageRipper.Gui.Models.Thin;

namespace NicheImageRipper.Gui.Services.Thin;

public class ThinGuiSettings : IGuiSettings
{
    private static GuiThinConfig Config => GuiThinConfig.Instance;
    
    public double NameWidth
    {
        get => Config.NameWidth;
        set => Config.NameWidth = value;
    }

    public double UrlWidth
    {
        get => Config.UrlWidth;
        set => Config.UrlWidth = value;
    }

    public double DateWidth
    {
        get => Config.DateWidth;
        set => Config.DateWidth = value;
    }

    public double CountWidth
    {
        get => Config.CountWidth;
        set => Config.CountWidth = value;
    }

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

    // Kept locally rather than on the backend: appearance is per client
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

    public void Save()
    {
        Config.Save();
    }
}