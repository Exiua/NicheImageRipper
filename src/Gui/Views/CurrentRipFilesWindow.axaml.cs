using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Gui.Services.Shared;
using NicheImageRipper.Gui.ViewModels;

namespace NicheImageRipper.Gui.Views;

public partial class CurrentRipFilesWindow : Window
{
    private CurrentRipFilesViewModel ViewModel => (CurrentRipFilesViewModel) DataContext!;

    public CurrentRipFilesWindow()
    {
        InitializeComponent();
        TitleBarTheme.Attach(this);
    }

    private async void CopyUrls(object? sender, RoutedEventArgs e)
    {
        try
        {
            var selected = FileList.SelectedItems?.OfType<RipFileEntry>().ToHashSet() ?? [];
            // Keep download order rather than the order they were selected in
            var files = selected.Count > 0 ? ViewModel.Files.Where(selected.Contains) : ViewModel.Files;
            if (Clipboard is not null)
            {
                await Clipboard.SetTextAsync(string.Join(Environment.NewLine, files.Select(f => f.Url)));
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
        }
    }

    private void Close(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
