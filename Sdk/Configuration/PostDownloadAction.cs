namespace Sdk.Configuration;

[Flags]
public enum PostDownloadAction
{
    None                = 0,
    RemoveDuplicates    = 1 << 0,
}