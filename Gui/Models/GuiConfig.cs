using Core.Configuration;
using JetBrains.Annotations;

namespace Gui.Models;

public class GuiConfig : GeneralConfig
{
    public HistoryColumnWidths HistoryColumnWidths { get; set; } = new();
}

public class HistoryColumnWidths
{
    public double NameWidth { get; set; } = 530;
    public double UrlWidth { get; set; } = 530;
    public double DateWidth { get; set; } = 150;
    public double CountWidth { get; set; } = 100;
}