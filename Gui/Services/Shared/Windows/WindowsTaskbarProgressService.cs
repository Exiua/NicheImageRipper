#if WINDOWS

using System;
using System.Runtime.InteropServices;

namespace Gui.Services.Shared.Windows;

public class WindowsTaskbarProgressService : ITaskbarProgressService
{
    //private ITaskbarList3 _taskbar;
    
    public void Initialize()
    {
        // Windows.Win32.P
        // _taskbar = (ITaskbarList3)new CLSID_TaskbarList();
        // _taskbar.HrInit();
    }
    
    public void SetProgress(IntPtr windowHandle, ulong current, ulong total)
    {
        // _taskbar.SetProgressState(hwnd, TBPFLAG.TBPF_NORMAL);
        // _taskbar.SetProgressValue(hwnd, 50, 100);
        TaskbarProgress.SetValue(windowHandle, current, total);
        if (current == 0 && total == 0)
        {
            TaskbarProgress.SetState(windowHandle, TaskbarProgress.TaskbarStates.Indeterminate);
        }
        else if (current < total)
        {
            TaskbarProgress.SetState(windowHandle, TaskbarProgress.TaskbarStates.Normal);
        }
        else
        {
            TaskbarProgress.SetState(windowHandle, TaskbarProgress.TaskbarStates.NoProgress);
        }
    }

    public void SetError(IntPtr windowHandle)
    {
        TaskbarProgress.SetState(windowHandle, TaskbarProgress.TaskbarStates.Error);
    }
    
    public void SetPaused(IntPtr windowHandle)
    {
        TaskbarProgress.SetState(windowHandle, TaskbarProgress.TaskbarStates.Paused);
    }
    
    public void ClearProgress(IntPtr windowHandle)
    {
        TaskbarProgress.SetState(windowHandle, TaskbarProgress.TaskbarStates.NoProgress);
    }
}

public static partial class TaskbarProgress
{
    public enum TaskbarStates
    {
        NoProgress    = 0,
        Indeterminate = 0x1,
        Normal        = 0x2,
        Error         = 0x4,
        Paused        = 0x8
    }

    [ComImport]
    [Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList3
    {
        // ITaskbarList
        [PreserveSig]
        void HrInit();
        [PreserveSig]
        void AddTab(IntPtr hwnd);
        [PreserveSig]
        void DeleteTab(IntPtr hwnd);
        [PreserveSig]
        void ActivateTab(IntPtr hwnd);
        [PreserveSig]
        void SetActiveAlt(IntPtr hwnd);

        // ITaskbarList2
        [PreserveSig]
        void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);

        // ITaskbarList3
        [PreserveSig]
        void SetProgressValue(IntPtr hwnd, UInt64 ullCompleted, UInt64 ullTotal);
        [PreserveSig]
        void SetProgressState(IntPtr hwnd, TaskbarStates state);
    }

    [ComImport]    
    [Guid("56fdf344-fd6d-11d0-958a-006097c9a090")]
    [ClassInterface(ClassInterfaceType.None)]
    internal class TaskbarInstance;

    private static readonly ITaskbarList3 _taskbarInstance = (ITaskbarList3)new TaskbarInstance();
    private static readonly bool TaskbarSupported = Environment.OSVersion.Version >= new Version(6, 1);

    public static void SetState(IntPtr windowHandle, TaskbarStates taskbarState)
    {
        if (TaskbarSupported)
        {
            _taskbarInstance.SetProgressState(windowHandle, taskbarState);
        }
    }

    public static void SetValue(IntPtr windowHandle, ulong progressValue, ulong progressMax)
    {
        if (TaskbarSupported)
        {
            _taskbarInstance.SetProgressValue(windowHandle, progressValue, progressMax);
        }
    }
}

#endif