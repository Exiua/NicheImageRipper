namespace MegaSdk;

/// <summary>
///     A file or folder in MEGA. Properties are read when the node is created; the node itself owns a native copy
///     that is used for downloads and listing children.
/// </summary>
public sealed class MegaNode : IDisposable
{
    public const ulong InvalidHandle = ulong.MaxValue;

    internal MegaNodeHandle Native { get; }

    public MegaNodeType Type { get; }
    public string Name { get; }
    public ulong Handle { get; }
    public ulong ParentHandle { get; }
    public long Size { get; }
    public DateTimeOffset CreationTime { get; }
    public DateTimeOffset ModificationTime { get; }
    public string? Fingerprint { get; }

    public bool IsFile => Type == MegaNodeType.File;
    public bool IsFolder => Type != MegaNodeType.File && Type != MegaNodeType.Unknown;

    /// <summary>
    ///     Base64 handle, as used in MEGA links.
    /// </summary>
    public string Base64Handle => MegaClient.HandleToBase64(Handle);

    private MegaNode(MegaNodeHandle native)
    {
        Native = native;
        Type = (MegaNodeType)NativeMethods.NodeGetType(native);
        Name = NativeMethods.ReadString(NativeMethods.NodeGetName(native)) ?? "";
        Handle = NativeMethods.NodeGetHandle(native);
        ParentHandle = NativeMethods.NodeGetParentHandle(native);
        Size = NativeMethods.NodeGetSize(native);
        CreationTime = DateTimeOffset.FromUnixTimeSeconds(NativeMethods.NodeGetCreationTime(native));
        ModificationTime = DateTimeOffset.FromUnixTimeSeconds(NativeMethods.NodeGetModificationTime(native));
        Fingerprint = NativeMethods.ReadString(NativeMethods.NodeGetFingerprint(native));
    }

    /// <summary>
    ///     Takes ownership of <paramref name="native" />. Returns null (and disposes it) if it's invalid.
    /// </summary>
    internal static MegaNode? FromNative(MegaNodeHandle native)
    {
        if (native.IsInvalid)
        {
            native.Dispose();
            return null;
        }

        return new MegaNode(native);
    }

    public override string ToString() => $"{Name} ({Type}, {Size} bytes)";

    public void Dispose()
    {
        Native.Dispose();
    }
}
