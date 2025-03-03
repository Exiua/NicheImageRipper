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
        Focusable = true;
        DataContext = new MainWindowViewModel();
        UrlQueue.ItemsSource = ViewModel.UrlQueue;
        FilenameSchemeComboBox.ItemsSource = Enum.GetValues<FilenameScheme>();
        FilenameSchemeComboBox.SelectedIndex = (int) NicheImageRipper.FilenameScheme;
        UnzipProtocolComboBox.ItemsSource = Enum.GetValues<UnzipProtocol>();
        UnzipProtocolComboBox.SelectedIndex = (int) NicheImageRipper.UnzipProtocol;
        //GuiSink.OnLog += OnLog;
        GuiSink.MainWindow = this;
        Closing += OnClose;
    }

    private void OnClose(object? sender, WindowClosingEventArgs windowClosingEventArgs)
    {
        try
        {
            ViewModel.SaveData();
        }
        finally
        {
            ViewModel.Cleanup();
        }
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
        ViewModel.DecrementHistoryPage();
        LoadHistory();
    }

    // FIXME: NextHistoryPageExists() is not working as expected
    private void NextHistoryPage(object? sender, RoutedEventArgs e)
    {
        ViewModel.IncrementHistoryPage();
        LoadHistory();
    }

    private void UpdateCurrentHistoryPage(object? sender, RoutedEventArgs e)
    {
        ViewModel.RefreshHistoryPage();
        LoadHistory();
    }

    private void LoadHistory()
    {
        ViewModel.LoadHistory();
        PreviousHistoryPageButton.IsEnabled = ViewModel.CurrentHistoryPageDisplay != "1";
        NextHistoryPageButton.IsEnabled = ViewModel.NextHistoryPageExists();
    }

    private void ValidateNumericValue(object? sender, RoutedEventArgs e)
    {
        var textBox = (TextBox) sender!;
        var rawValue = textBox.Text;
        if (!int.TryParse(rawValue, out var value) || value < 0)
        {
            value = -1;
        }

        switch (textBox.Name)
        {
            case "MaxRetriesTextBox":
                ViewModel.SetMaxRetries(value);
                break;
            case "RetryDelayTextBox":
                ViewModel.SetRetryDelay(value);
                break;
        }
    }
}