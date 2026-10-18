using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NicheImageRipper.Gui.ViewModels;

namespace NicheImageRipper.Gui.Views;

public partial class SiteConfigWindow : Window
{
    private SiteConfigEditorViewModel ViewModel => (SiteConfigEditorViewModel) DataContext!;

    public SiteConfigWindow()
    {
        InitializeComponent();
    }

    private void Save(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.Validate())
        {
            Close(true);
        }
    }

    private void Cancel(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private async void BrowsePostProcessScript(object? sender, RoutedEventArgs e)
    {
        try
        {
            var site = ViewModel.SelectedSite;
            if (site is null)
            {
                return;
            }

            var currentDirectory = Path.GetDirectoryName(site.PostProcessScript);
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = $"Select Post-Process Script for {site.Name}",
                AllowMultiple = false,
                SuggestedStartLocation = string.IsNullOrEmpty(currentDirectory)
                    ? null
                    : await StorageProvider.TryGetFolderFromPathAsync(currentDirectory),
            });
            if (files.Count == 0)
            {
                return;
            }

            site.PostProcessScript = files[0].TryGetLocalPath() ?? Uri.UnescapeDataString(files[0].Path.AbsolutePath);
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
        }
    }
}
