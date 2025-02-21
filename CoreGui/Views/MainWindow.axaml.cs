using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Core;
using Core.Enums;
using CoreGui.Utility;
using CoreGui.ViewModels;
using Serilog;

namespace CoreGui.Views;

public partial class MainWindow : Window
{
    private static FilePickerFileType Json { get; } = new("JSON")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"],
    };
    
    private MainWindowViewModel ViewModel => (MainWindowViewModel) DataContext!;
    
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        UrlQueue.ItemsSource = ViewModel.UrlQueue;
        FilenameSchemeComboBox.ItemsSource = Enum.GetValues<FilenameScheme>();
        FilenameSchemeComboBox.SelectedIndex = (int) NicheImageRipper.FilenameScheme;
        UnzipProtocolComboBox.ItemsSource = Enum.GetValues<UnzipProtocol>();
        UnzipProtocolComboBox.SelectedIndex = (int) NicheImageRipper.UnzipProtocol;
        //GuiSink.OnLog += OnLog;
        GuiSink.MainWindow = this;
    }

    public void OnLog(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ViewModel.LogText += message + Environment.NewLine;
            LogTextBox.CaretIndex = int.MaxValue;
        });
    }
    
    private async void SelectFolder(object? sender, RoutedEventArgs routedEventArgs)
    {
        try
        {
            var folder = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Directory",
                SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(NicheImageRipper.SavePath)
            });
            
            if (folder.Count == 0)
            {
                return;
            }
            
            var path = Uri.UnescapeDataString(folder[0].Path.AbsolutePath);
            Log.Debug("Selected folder: {folder}", path);
            ViewModel.SavePath = path;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to open folder picker");
        }
    }

    private async void SelectUnfinishedUrlFile(object? sender, RoutedEventArgs e)
    {
        try
        {
            var file = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Unfinished URL File",
                FileTypeFilter = [ Json ],
                SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(".") // TODO: Change to executable path
            });
            
            if (file.Count == 0)
            {
                return;
            }
            
            var path = Uri.UnescapeDataString(file[0].Path.AbsolutePath);
            Log.Debug("Selected file: {file}", path);
            ViewModel.LoadUnfinishedUrls(path);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to open file picker");
        }
    }

    private void PreviousHistoryPage(object? sender, RoutedEventArgs e)
    {
        ViewModel.CurrentHistoryPage--;
        ViewModel.LoadHistory();
        if (ViewModel.CurrentHistoryPage <= 0)
        {
            PreviousHistoryPageButton.IsEnabled = false;
        }
        if (ViewModel.NextHistoryPageExists())
        {
            NextHistoryPageButton.IsEnabled = true;
        }
    }

    // FIXME: NextHistoryPageExists() is not working as expected
    private void NextHistoryPage(object? sender, RoutedEventArgs e)
    {
        ViewModel.CurrentHistoryPage++;
        ViewModel.LoadHistory();
        if (ViewModel.CurrentHistoryPage > 0)
        {
            PreviousHistoryPageButton.IsEnabled = true;
        }
        if (!ViewModel.NextHistoryPageExists())
        {
            NextHistoryPageButton.IsEnabled = false;
        }
    }
}