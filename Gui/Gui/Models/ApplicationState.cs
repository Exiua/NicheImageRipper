namespace Gui.Models;

public class ApplicationState
{
    public static ApplicationState Instance { get; } = new();

    private ApplicationState()
    {
        
    }
}