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
}