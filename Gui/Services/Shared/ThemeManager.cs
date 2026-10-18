using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace NicheImageRipper.Gui.Services.Shared;

/// <summary>Applies the theme variant and accent color chosen in Settings, live, to the whole application.</summary>
public static class ThemeManager
{
    public const string System = "System";
    public const string Light = "Light";
    public const string Dark = "Dark";

    public static IReadOnlyList<string> Themes { get; } = [System, Light, Dark];

    // Fluent derives its accent brushes from these keys via DynamicResource, so overriding them in
    // Application.Resources recolors every accent control immediately
    private static readonly string[] AccentKeys =
    [
        "SystemAccentColor",
        "SystemAccentColorLight1", "SystemAccentColorLight2", "SystemAccentColorLight3",
        "SystemAccentColorDark1", "SystemAccentColorDark2", "SystemAccentColorDark3",
    ];

    public static void ApplyTheme(string? theme)
    {
        if (Application.Current is not { } app)
        {
            return;
        }

        app.RequestedThemeVariant = theme switch
        {
            Light => ThemeVariant.Light,
            Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default, // follows the OS
        };
    }

    /// <param name="accentHex">A color like "#378ADD", or null/empty to use the OS accent color.</param>
    public static void ApplyAccent(string? accentHex)
    {
        if (Application.Current is not { } app)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(accentHex) || !Color.TryParse(accentHex, out var accent))
        {
            foreach (var key in AccentKeys)
            {
                app.Resources.Remove(key);
            }

            return;
        }

        app.Resources["SystemAccentColor"] = accent;
        app.Resources["SystemAccentColorLight1"] = Mix(accent, Colors.White, 0.15);
        app.Resources["SystemAccentColorLight2"] = Mix(accent, Colors.White, 0.30);
        app.Resources["SystemAccentColorLight3"] = Mix(accent, Colors.White, 0.45);
        app.Resources["SystemAccentColorDark1"] = Mix(accent, Colors.Black, 0.15);
        app.Resources["SystemAccentColorDark2"] = Mix(accent, Colors.Black, 0.30);
        app.Resources["SystemAccentColorDark3"] = Mix(accent, Colors.Black, 0.45);
    }

    private static Color Mix(Color from, Color to, double amount)
    {
        byte Lerp(byte a, byte b) => (byte)Math.Round(a + (b - a) * amount);
        return Color.FromArgb(255, Lerp(from.R, to.R), Lerp(from.G, to.G), Lerp(from.B, to.B));
    }
}

/// <param name="Hex">Null means "use the OS accent color"</param>
public sealed record AccentOption(string Name, string? Hex)
{
    public bool HasColor => Hex is not null;
    public IBrush Brush => Hex is null ? Brushes.Transparent : SolidColorBrush.Parse(Hex);

    public static IReadOnlyList<AccentOption> All { get; } =
    [
        new("System", null),
        new("Blue", "#378ADD"),
        new("Teal", "#1D9E75"),
        new("Purple", "#7F77DD"),
        new("Pink", "#D4537E"),
        new("Coral", "#D85A30"),
        new("Amber", "#BA7517"),
        new("Green", "#639922"),
    ];
}
