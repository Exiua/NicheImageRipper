namespace NicheImageRipper.Core.DataStructures;

public class Box<T>(T value)
{
    public T Value { get; set; } = value;
    
    public static implicit operator T(Box<T> box) => box.Value;
}