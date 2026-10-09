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
}