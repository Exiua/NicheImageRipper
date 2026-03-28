using Core.Configuration;
using JetBrains.Annotations;

namespace Gui.Models;

public class GuiConfig : GeneralConfig
{
    public HistoryColumnWidths HistoryColumnWidths { get; set; } = null!;
    
    [UsedImplicitly]
    public GuiConfig() : base()
    {
        
    }

    protected GuiConfig(bool _) : base(_)
    {
        HistoryColumnWidths = HistoryColumnWidths.Default;
    }
}

public class HistoryColumnWidths
{
    public double NameWidth { get; set; }
    public double UrlWidth { get; set; }
    public double DateWidth { get; set; }
    public double CountWidth { get; set; }
    
    [UsedImplicitly]
    public HistoryColumnWidths()
    {
        
    }
    
    public static HistoryColumnWidths Default { get; } = new()
    {
        NameWidth = 530,
        UrlWidth = 530,
        DateWidth = 150,
        CountWidth = 100
    };
}