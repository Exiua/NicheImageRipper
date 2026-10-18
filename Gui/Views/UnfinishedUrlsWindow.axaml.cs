using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NicheImageRipper.Gui.Services.Shared;
using NicheImageRipper.Gui.ViewModels;

namespace NicheImageRipper.Gui.Views;

/// <summary>Closes with the full path of the file to load, or null when cancelled.</summary>
public partial class UnfinishedUrlsWindow : Window
{
    private static readonly FilePickerFileType Json = new("JSON URL list")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"],
    };

    private UnfinishedUrlsViewModel ViewModel => (UnfinishedUrlsViewModel) DataContext!;

    public UnfinishedUrlsWindow()
    {
        InitializeComponent();
        TitleBarTheme.Attach(this);
    }

    private async void BrowseFile(object? sender, RoutedEventArgs e)
    {
        try
        {
            var startDirectory = Directory.Exists(ViewModel.DirectoryPath) ? ViewModel.DirectoryPath : null;
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select a URL list",
                AllowMultiple = false,
                FileTypeFilter = [Json],
                SuggestedStartLocation = startDirectory is null
                    ? null
                    : await StorageProvider.TryGetFolderFromPathAsync(startDirectory),
            });
            if (files.Count == 0)
            {
                return;
            }

            var path = files[0].TryGetLocalPath() ?? Uri.UnescapeDataString(files[0].Path.AbsolutePath);
            ViewModel.AddExternalFile(path);
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
        }
    }

    private void Load(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.CanLoad)
        {
            Close(ViewModel.SelectedFile!.FullPath);
        }
    }

    private void Cancel(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
