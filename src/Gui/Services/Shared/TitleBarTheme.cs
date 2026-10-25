using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace NicheImageRipper.Gui.Services.Shared;

/// <summary>
///     Keeps the native title bar in step with the app's theme. Avalonia follows the OS light/dark setting for the
///     title bar, so choosing Dark in Settings on a light OS left a white title bar. Windows only: macOS and Linux
///     window managers draw their own decorations.
/// </summary>
public static class TitleBarTheme
{
    private const int DwmUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmCaptionColor = 35; // Windows 11+
    private const int DwmTextColor = 36;    // Windows 11+

    public static void Attach(Window window)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            return;
        }

        // The native window already exists after construction, so apply before it's first shown, then on changes
        Apply(window);
        window.Opened += (_, _) => Apply(window);
        window.ActualThemeVariantChanged += (_, _) => Apply(window);
    }

    private static void Apply(Window window)
    {
        var hwnd = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var dark = window.ActualThemeVariant == ThemeVariant.Dark ? 1 : 0;
        var darkModeAttribute = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041)
            ? DwmUseImmersiveDarkMode
            : DwmUseImmersiveDarkModeBefore20H1;
        DwmSetWindowAttribute(hwnd, darkModeAttribute, ref dark, sizeof(int));

        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            // Windows 11 can color the caption exactly, so match the window's own background and text
            if (TryGetColor(window, "SystemRegionColor", out var background))
            {
                var captionColor = ToColorRef(background);
                DwmSetWindowAttribute(hwnd, DwmCaptionColor, ref captionColor, sizeof(int));
            }

            if (TryGetColor(window, "SystemBaseHighColor", out var foreground))
            {
                var textColor = ToColorRef(foreground);
                DwmSetWindowAttribute(hwnd, DwmTextColor, ref textColor, sizeof(int));
            }
        }

        // Windows 10 only repaints the caption with the new colors when its active state changes, so resend the
        // current state to redraw it now rather than on the next focus change. Doesn't move focus.
        var isActive = GetForegroundWindow() == hwnd;
        if (isActive)
        {
            SendMessage(hwnd, WmNcActivate, IntPtr.Zero, IntPtr.Zero);
        }

        SendMessage(hwnd, WmNcActivate, isActive ? 1 : IntPtr.Zero, IntPtr.Zero);
    }

    private static bool TryGetColor(Window window, string key, out Color color)
    {
        if (window.TryFindResource(key, window.ActualThemeVariant, out var value) && value is Color found)
        {
            color = found;
            return true;
        }

        color = default;
        return false;
    }

    private static int ToColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16); // 0x00BBGGRR

    private const int WmNcActivate = 0x0086;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
}
