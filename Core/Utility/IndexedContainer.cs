using System.Text.Json.Serialization;

namespace NicheImageRipper.Core.Utility;

public class IndexedContainer<T>
{
    public int Index { get; set; }
    public T Value { get; set; } = default!;

    [JsonConstructor]
    public IndexedContainer()
    {
        
    }
    
    public IndexedContainer(T value, int index)
    {
        Index = index;
        Value = value;
    }
    
    public void Deconstruct(out T value, out int index)
    {
        value = Value;
        index = Index;
    }
}