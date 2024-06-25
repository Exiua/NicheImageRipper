namespace Core.DataStructures;

public class LazyLoadArgs
{
    public bool ScrollBy { get; set; } = false;
    public int Increment { get; set; } = 2500;
    public int ScrollPauseTime { get; set; } = 500;
    public int ScrollBack { get; set; } = 0;
    public bool ReScroll { get; set; } = false;
}