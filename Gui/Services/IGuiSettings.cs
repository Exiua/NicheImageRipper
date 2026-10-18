namespace NicheImageRipper.Gui.Services;

public interface IGuiSettings : IAsyncInitialization
{
    public double NameWidth { get; set; }
    public double UrlWidth { get; set; }
    public double DateWidth { get; set; }
    public double CountWidth { get; set; }

    public bool ShowLogTimestampAndLevel { get; set; }
    public bool ShowDebugLogs { get; set; }
    public bool WrapLogLines { get; set; }

    /// <summary>Persists GUI-only settings that aren't saved along with the ripper config.</summary>
    public void Save()
    {
    }
}
