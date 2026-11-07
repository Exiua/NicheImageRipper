using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace NicheImageRipper.Gui.Services.Shared.Windows;

/// <summary>
/// Taskbar progress via ITaskbarList3. TaskbarList is an apartment-threaded COM object, so <see cref="Initialize"/>
/// and every other call must happen on the UI (STA) thread.
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsTaskbarProgressService : ITaskbarProgressService
{
    private ITaskbarList3? _taskbar;

    public void Initialize()
    {
        if (_taskbar is not null || Environment.OSVersion.Version < new Version(6, 1))
        {
            return;
        }

        var taskbar = (ITaskbarList3)new TaskbarInstance();
        // Must succeed before any other ITaskbarList method is called
        if (taskbar.HrInit() >= 0)
        {
            _taskbar = taskbar;
        }
    }

    public void SetProgress(IntPtr windowHandle, ulong current, ulong total)
    {
        if (current == 0 && total == 0)
        {
            SetState(windowHandle, TaskbarStates.Indeterminate);
        }
        else if (current < total)
        {
            // Setting the value also switches a NoProgress/Indeterminate bar back to Normal
            _taskbar?.SetProgressValue(windowHandle, current, total);
            SetState(windowHandle, TaskbarStates.Normal);
        }
        else
        {
            SetState(windowHandle, TaskbarStates.NoProgress);
        }
    }

    public void SetError(IntPtr windowHandle)
    {
        SetState(windowHandle, TaskbarStates.Error);
    }

    public void SetPaused(IntPtr windowHandle)
    {
        SetState(windowHandle, TaskbarStates.Paused);
    }

    public void ClearProgress(IntPtr windowHandle)
    {
        SetState(windowHandle, TaskbarStates.NoProgress);
    }

    private void SetState(IntPtr windowHandle, TaskbarStates state)
    {
        _taskbar?.SetProgressState(windowHandle, state);
    }

    private enum TaskbarStates
    {
        NoProgress = 0,
        Indeterminate = 0x1,
        Normal = 0x2,
        Error = 0x4,
        Paused = 0x8
    }

    [ComImport]
    [Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList3
    {
        // ITaskbarList
        [PreserveSig]
        int HrInit();

        [PreserveSig]
        int AddTab(IntPtr hwnd);

        [PreserveSig]
        int DeleteTab(IntPtr hwnd);

        [PreserveSig]
        int ActivateTab(IntPtr hwnd);

        [PreserveSig]
        int SetActiveAlt(IntPtr hwnd);

        // ITaskbarList2
        [PreserveSig]
        int MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);

        // ITaskbarList3
        [PreserveSig]
        int SetProgressValue(IntPtr hwnd, ulong ullCompleted, ulong ullTotal);

        [PreserveSig]
        int SetProgressState(IntPtr hwnd, TaskbarStates state);
    }

    [ComImport]
    [Guid("56fdf344-fd6d-11d0-958a-006097c9a090")]
    [ClassInterface(ClassInterfaceType.None)]
    private class TaskbarInstance;
}
