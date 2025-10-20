namespace Core.DataStructures;

public class CompactBoolVector
{
    private readonly int[] _flags;
    
    public bool this[int index]
    {
        get => (_flags[index / 32] & (1 << (index % 32))) != 0;
        set
        {
            if (value)
            {
                _flags[index / 32] |= 1 << (index % 32);
            }
            else
            {
                _flags[index / 32] &= ~(1 << (index % 32));
            }
        }
    }

    public CompactBoolVector(int size)
    {
        var arraySize = size / 32 + 1;
        _flags = new int[arraySize];
    }
}