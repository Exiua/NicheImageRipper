using System;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using GuiThin.ViewModels;
using ReactiveUI.Avalonia;
using Serilog;

namespace GuiThin.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    private static FilePickerFileType Json { get; } = new("JSON")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"],
    };
    
    public MainWindow()
    {
        ViewModel!.MainWindow = this;
        InitializeComponent();
    }
    
    protected override async void OnOpened(EventArgs eventArgs)
    {
        try
        {
            base.OnOpened(eventArgs);

            if (ViewModel is not null)
            {
                await ViewModel.RefreshStateAsync();
                UpdatePlayPauseIcon(ViewModel.Paused);
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "An error occured while refreshing the state.");
        }
    }
    
    private async void PlayPauseButton_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        try
        {
            if (ViewModel is null)
            {
                return;
            }

            if (ViewModel.Paused)
            {
                await ViewModel.PlayAsync();
            }
            else
            {
                await ViewModel.PauseAsync();
            }

            UpdatePlayPauseIcon(ViewModel.Paused);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error toggling play/pause");
        }
    }
    
    private void UpdatePlayPauseIcon(bool paused)
    {
        var resourceKey = paused ? "PlayButtonIcon" : "PauseButtonIcon";

        var iconFound = TryGetResource(resourceKey, null, out var icon);
        if (iconFound)
        {
            PlayPauseButtonIcon.Data = (StreamGeometry)icon!;
        }
    }
}