using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Core;
using Core.Enums;
using Core.History;
using Gui.Models;
using Gui.Utility;
using Gui.ViewModels;
using ReactiveUI;
using Serilog;

namespace Gui.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    private static FilePickerFileType Json { get; } = new("JSON")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"],
    };

    private Key _lastKeyPressed;
    private bool _copyReady;

    public MainWindow()
    {
        DataContext = new MainWindowViewModel
        {
            MainWindow = this,
        };
        
        // Needs to be called after DataContext is set otherwise it messes up initial values for components and callbacks
        InitializeComponent();
        Focusable = true;
        UrlQueue.ItemsSource = ViewModel!.UrlQueue;
        FilenameSchemeComboBox.ItemsSource = Enum.GetValues<FilenameScheme>();
        FilenameSchemeComboBox.SelectedIndex = (int)NicheImageRipper.FilenameScheme;
        UnzipProtocolComboBox.ItemsSource = Enum.GetValues<UnzipProtocol>();
        UnzipProtocolComboBox.SelectedIndex = (int)NicheImageRipper.UnzipProtocol;
        //GuiSink.OnLog += OnLog;
        GuiSink.MainWindow = this;
        Closing += OnClose;
        this.WhenActivated(action =>
            action(ViewModel.ShowConfirmationDialog.RegisterHandler(DoShowDialogAsync)));
    }

    private void OnClose(object? sender, WindowClosingEventArgs windowClosingEventArgs)
    {
        var task = Dispatcher.UIThread.InvokeAsync(() =>
        {
            try
            {
                ViewModel!.SaveData();
            }
            finally
            {
                ViewModel!.Cleanup();
            }
        });
        
        task.Wait();
    }
    
    private readonly ThreadSafeStringBuilder _logBuilder = new();
    private readonly Mutex _logLock = new();
    private int _logCount;
    private const int MaxLogLines = 1000;
    private const int NumLogLinesToRemove = 50;
    private bool _paused;

    public void OnLog(string message)
    {
        lock (_logLock)
        {
            _logBuilder.Append($"{message}\n");
            _logCount++;
            if (_logCount > MaxLogLines)
            {
                var lines = 0;
                for (var i = 0; i < _logBuilder.Length; i++)
                {
                    if (_logBuilder[i] == '\n')
                    {
                        lines++;
                    }
                
                    if (lines >= NumLogLinesToRemove)
                    {
                        _logBuilder.Remove(0, i + 1); // Works because we are removing the first i + 1 characters
                        _logCount -= lines;
                        break;
                    }
                
                    // This can technically never remove lines from the log if for some reason, less than NumLogLinesToRemove
                    // lines are in the log, but that **should** never happen
                }
            }
            
            Dispatcher.UIThread.Post(() =>
            {
                ViewModel!.LogText = _logBuilder.ToString();
                LogTextBox.CaretIndex = int.MaxValue;
            }, DispatcherPriority.Background);
        }
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
            ViewModel!.SavePath = path;
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
                FileTypeFilter = [Json],
                SuggestedStartLocation =
                    await StorageProvider.TryGetFolderFromPathAsync(".") // TODO: Change to executable path
            });

            if (file.Count == 0)
            {
                return;
            }

            var path = Uri.UnescapeDataString(file[0].Path.AbsolutePath);
            Log.Debug("Selected file: {file}", path);
            ViewModel!.LoadUnfinishedUrls(path);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to open file picker");
        }
    }

    private void PreviousHistoryPage(object? sender, RoutedEventArgs e)
    {
        ViewModel!.DecrementHistoryPage();
        LoadHistory();
    }

    // FIXME: NextHistoryPageExists() is not working as expected
    private void NextHistoryPage(object? sender, RoutedEventArgs e)
    {
        ViewModel!.IncrementHistoryPage();
        LoadHistory();
    }

    private void UpdateCurrentHistoryPage(object? sender, RoutedEventArgs e)
    {
        ViewModel!.RefreshHistoryPage();
        LoadHistory();
    }

    private void UpdateCurrentHistoryPageWithFilter(object? send, RoutedEventArgs e)
    {
        var input = ViewModel!.HistoryFilterText.Trim();
        if (string.IsNullOrEmpty(input))
        {
            ViewModel.ClearHistoryFilter();
            LoadHistory();
            return;
        }
        
        var filter = HistoryFilter.Parse(input);
        LoadHistory(filter);
    }

    private static DateTime? ParsePartialDate(string input)
    {
        var formats = new[]
        {
            "yyyy",       // e.g., "2025" → 2025/01/01
            "MM",         // e.g., "02"   → currentYear/02/01
            "yyyy/MM",    // e.g., "2025/02" → 2025/02/01
            "yyyy-MM",    // e.g., "2025-02"
            "MM/yyyy",    // e.g., "02/2025"
            "MM-yyyy",    // e.g., "02-2025"
            "yyyy/MM/dd", // full date fallback
            "MM/dd/yyyy"
        };

        var now = DateTime.Now;

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(input, format, null, System.Globalization.DateTimeStyles.None, out var result))
            {
                return format switch
                {
                    // Fill in missing components manually
                    "yyyy" => new DateTime(result.Year, 1, 1),
                    "MM" => new DateTime(now.Year, result.Month, 1),
                    "yyyy/MM" or "yyyy-MM" or "MM/yyyy" or "MM-yyyy" => new DateTime(result.Year, result.Month, 1),
                    _ => result
                };
            }
        }

        return null;
    }
    
    private void ClearHistoryFilter(object? sender, RoutedEventArgs e)
    {
        ViewModel!.ClearHistoryFilter();
        LoadHistory();
    }

    private void LoadHistory(HistoryFilter? filter = null)
    {
        ViewModel!.LoadHistory(filter);
        PreviousHistoryPageButton.IsEnabled = ViewModel.CurrentHistoryPageDisplay != "1";
        NextHistoryPageButton.IsEnabled = ViewModel.NextHistoryPageExists();
    }

    private void ValidateNumericValue(object? sender, RoutedEventArgs e)
    {
        var textBox = (TextBox)sender!;
        var rawValue = textBox.Text;
        if (!int.TryParse(rawValue, out var value) || value < 0)
        {
            value = -1;
        }

        switch (textBox.Name)
        {
            case "MaxRetriesTextBox":
                ViewModel!.SetMaxRetries(value);
                break;
            case "RetryDelayTextBox":
                ViewModel!.SetRetryDelay(value);
                break;
        }
    }

    private async Task DoShowDialogAsync(IInteractionContext<ConfirmationViewModel, ConfirmationViewModel?> interaction)
    {
        var dialog = new ConfirmationWindow
        {
            DataContext = interaction.Input
        };

        var result = await dialog.ShowDialog<ConfirmationViewModel?>(this);
        interaction.SetOutput(result);
    }

    private void OnUrlInput(object? sender, TextChangedEventArgs textChangedEventArgs)
    {
        var textBox = sender as TextBox;
        if (textBox?.Text is null)
        {
            return;
        }

        var urlInput = textBox.Text;
        urlInput = urlInput.Replace("\n", "").Replace("\r", "");
        GridBackground.Focus();
        DispatcherTimer.RunOnce(() =>
        {
            ViewModel!.UrlInput = urlInput;
            textBox.Focus();
        }, TimeSpan.FromMilliseconds(10));
    }

    private void OnHistoryColumnWidthChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e is not AvaloniaPropertyChangedEventArgs<DataGridLength> dataGridLengthArgs || sender is not DataGridTextColumn column || ViewModel is null)
        {
            return;
        }

        switch (column.Tag)
        {
            case "HistoryName":
                ViewModel.NameWidth = dataGridLengthArgs.NewValue.Value.Value;
                break;
            case "HistoryUrl":
                ViewModel.UrlWidth = dataGridLengthArgs.NewValue.Value.Value;
                break;
            case "HistoryDate":
                ViewModel.DateWidth = dataGridLengthArgs.NewValue.Value.Value;
                break;
            case "HistoryCount":
                ViewModel.CountWidth = dataGridLengthArgs.NewValue.Value.Value;
                break;
            default:
                Log.Warning("Unknown column tag: {tag}", column.Tag);
                break;
        }
    }
    
    private void HistoryDataGrid_OnKeyUp(object? sender, KeyEventArgs e)
    {
        Log.Debug("Key Up: {Key}", e.Key);
        if (sender is not DataGrid dataGrid)
        {
            return;
        }

        _copyReady = _lastKeyPressed switch
        {
            Key.LeftCtrl or Key.RightCtrl => e.Key == Key.C,
            Key.C => e.Key is Key.LeftCtrl or Key.RightCtrl,
            _ => _copyReady
        };

        Log.Debug("Copy Ready: {CopyReady}", _copyReady);
        _lastKeyPressed = e.Key;

        if (_copyReady)
        {
            var descendants = dataGrid.GetVisualDescendants();
            foreach (var cell in descendants.OfType<DataGridCell>())
            {
                if (!cell.Classes.Contains(":selected"))
                {
                    continue;
                }
                
                var textBlock = cell.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault();
                if (textBlock is not null)
                {
                    Log.Debug("Copy: {Text}", textBlock.Text);
                    Clipboard?.SetTextAsync(textBlock.Text).Wait();
                }
            }
            
            _copyReady = false;
        }
    }
    
    private void PlayPauseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        PlayPauseToggle();
    }
    
    private void PlayPauseToggle()
    {
        if (_paused)
        {
            Play();
            _paused = false;
        }
        else
        {
            Pause();
            _paused = true;
        }
    }

    private void Play()
    {
        var iconFound = this.TryGetResource("PauseButtonIcon", out var icon);
        if (iconFound)
        {
            PlayPauseButtonIcon.Data = (StreamGeometry)icon!;
        }

        var playing = ViewModel?.Play() ?? false;
        _paused = !playing;
    }

    private void Pause()
    {
        var iconFound = this.TryGetResource("PlayButtonIcon", out var icon);
        if (iconFound)
        {
            PlayPauseButtonIcon.Data = (StreamGeometry)icon!;
        }
        
        var paused = ViewModel?.Pause() ?? false;
        _paused = paused;
    }
}