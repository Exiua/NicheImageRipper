using System;

namespace Gui.Models;

public interface IWindowProgressHost
{
    protected ITaskbarProgressService ProgressService { get; }
    protected IntPtr WindowHandle { get; }

    public void Initialize()
    {
        ProgressService.Initialize();
    }

    public void SetProgress(ulong current, ulong total)
    {
        if (WindowHandle != IntPtr.Zero)
        {
            ProgressService.SetProgress(WindowHandle, current, total);
        }
    }

    public void SetError()
    {
        if (WindowHandle != IntPtr.Zero)
        {
            ProgressService.SetError(WindowHandle);
        }
    }
    
    public void SetPaused()
    {
        if (WindowHandle != IntPtr.Zero)
        {
            ProgressService.SetPaused(WindowHandle);
        }
    }

    public void ClearProgress()
    {
        if (WindowHandle != IntPtr.Zero)
        {
            ProgressService.ClearProgress(WindowHandle);
        }
    }

}