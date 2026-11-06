using Microsoft.Win32.SafeHandles;

namespace MegaSdk;

/// <summary>
///     Owns a native MegaNode copy.
/// </summary>
internal sealed class MegaNodeHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public MegaNodeHandle() : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle()
    {
        NativeMethods.NodeFree(handle);
        return true;
    }
}
