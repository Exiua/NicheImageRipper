namespace Core.Enums;

[Flags]
public enum ModifiedHeader
{
    None            = 0,
    Authorization   = 1 << 0,
    Cookie          = 1 << 1,
}