namespace NicheImageRipper.Gui.Services;

public interface IGuiSettings : IAsyncInitialization
{
    public double NameWidth { get; set; }
    public double UrlWidth { get; set; }
    public double DateWidth { get; set; }
    public double CountWidth { get; set; }
}