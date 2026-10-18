using NicheImageRipper.Sdk.Configuration;

namespace NicheImageRipper.Gui.Models;

public class GuiConfig : GeneralConfig
{
    public HistoryColumnWidths HistoryColumnWidths { get; set; } = new();
    public LogDisplaySettings LogDisplay { get; set; } = new();
}

/// <summary>How the log tab renders entries; the log file is unaffected and always has full detail.</summary>
public class LogDisplaySettings
{
    public bool ShowTimestampAndLevel { get; set; } = false;
    public bool ShowDebug { get; set; } = false;
    public bool WrapLines { get; set; } = false;
}

public class HistoryColumnWidths
{
    public double NameWidth { get; set; } = 530;
    public double UrlWidth { get; set; } = 530;
    public double DateWidth { get; set; } = 150;
    public double CountWidth { get; set; } = 100;
}