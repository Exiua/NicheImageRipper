namespace Core.DataStructures;

public class TokenRotation
{
    public DateTime LastUsed { get; set; } = DateTime.Now;
    public TimeSpan UsedDuration { get; set; } = TimeSpan.Zero;
    public int CurrentIndex { get; set; }
    public bool Paused { get; set; } = true;
}