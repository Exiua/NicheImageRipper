namespace Core.Exceptions;

public class EnumOutOfRangeException : RipperException
{
    public object Value { get; }
    public Type EnumType { get; }

    public EnumOutOfRangeException(object value, Type enumType)
        : base($"Value '{value}' is out of range for enum '{enumType.Name}'.")
    {
        Value = value;
        EnumType = enumType;
    }
}