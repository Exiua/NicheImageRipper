using System;

namespace NicheImageRipper.Gui.Services.Shared;

public sealed class NullTaskbarProgressService : ITaskbarProgressService
{
    public void Initialize() { }
    public void SetProgress(IntPtr windowHandle, ulong current, ulong total) { }
    public void SetError(IntPtr windowHandle) { }
    public void SetPaused(IntPtr windowHandle) { }
    public void ClearProgress(IntPtr windowHandle) { }
}