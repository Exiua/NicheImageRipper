using System;

namespace NicheImageRipper.Gui.Services;

public interface ITaskbarProgressService
{
    void Initialize();
    void SetProgress(IntPtr windowHandle, ulong current, ulong total);
    void SetError(IntPtr windowHandle);
    void SetPaused(IntPtr windowHandle);
    void ClearProgress(IntPtr windowHandle);
}