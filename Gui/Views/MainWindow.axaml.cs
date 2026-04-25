using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
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
using Gui.Services;
using Gui.Utility;
using Gui.ViewModels;
using ReactiveUI;
using Serilog;

namespace Gui.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModelBase>
{
    private static FilePickerFileType Json { get; } = new("JSON")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"],
    };

    private readonly ITaskbarProgressService _taskbarProgressService;

    private Key _lastKeyPressed;
    private bool _copyReady;
    private IntPtr _windowHandle;

    public MainWindow(MainWindowViewModelBase viewModel, ITaskbarProgressService taskbarProgressService)
    {
        _taskbarProgressService = taskbarProgressService;
        DataContext = viewModel;
        viewModel.Initialize();
        
        // Needs to be called after DataContext is set otherwise it messes up initial values for components and callbacks
        InitializeComponent();
        Focusable = true;
        UrlQueue.ItemsSource = ViewModel!.UrlQueue;
        FilenameSchemeComboBox.ItemsSource = Enum.GetValues<FilenameScheme>();
        FilenameSchemeComboBox.SelectedIndex = (int)NicheImageRipper.FilenameScheme;
        UnzipProtocolComboBox.ItemsSource = Enum.GetValues<UnzipProtocol>();
        UnzipProtocolComboBox.SelectedIndex = (int)NicheImageRipper.UnzipProtocol;
        
        Closing += OnClosing;
        Opened += OnOpened;
        
        this.WhenActivated(disposables =>
        {
            ViewModel!.ShowConfirmationDialog.RegisterHandler(DoShowDialogAsync).DisposeWith(disposables);
        });
        
        this.WhenActivated(disposables =>
        {
            ViewModel!.WhenAnyValue(x => x.ProgressCurrent, x => x.ProgressTotal)
                .Subscribe(tuple =>
                {
                    var (current, total) = tuple;

                    if (_windowHandle == IntPtr.Zero)
                    {
                        return;
                    }

                    if (total == 0)
                    {
                        _taskbarProgressService.ClearProgress(_windowHandle);
                    }
                    else
                    {
                        _taskbarProgressService.SetProgress(_windowHandle, current, total);
                    }
                })
                .DisposeWith(disposables);

            ViewModel!.WhenAnyValue(x => x.ProgressHasError)
                .Subscribe(hasError =>
                {
                    if (_windowHandle == IntPtr.Zero)
                    {
                        return;
                    }

                    if (hasError)
                    {
                        _taskbarProgressService.SetError(_windowHandle);
                    }
                    else if (ViewModel.ProgressTotal == 0)
                    {
                        _taskbarProgressService.ClearProgress(_windowHandle);
                    }
                    else if (ViewModel.ProgressIsPaused)
                    {
                        _taskbarProgressService.SetPaused(_windowHandle);
                    }
                    else
                    {
                        _taskbarProgressService.SetProgress(
                            _windowHandle,
                            ViewModel.ProgressCurrent,
                            ViewModel.ProgressTotal);
                    }
                })
                .DisposeWith(disposables);

            ViewModel!.WhenAnyValue(x => x.ProgressIsPaused)
                .Subscribe(isPaused =>
                {
                    if (_windowHandle == IntPtr.Zero || ViewModel.ProgressTotal == 0 || ViewModel.ProgressHasError)
                    {
                        return;
                    }

                    if (isPaused)
                    {
                        _taskbarProgressService.SetPaused(_windowHandle);
                    }
                    else
                    {
                        _taskbarProgressService.SetProgress(
                            _windowHandle,
                            ViewModel.ProgressCurrent,
                            ViewModel.ProgressTotal);
                    }
                })
                .DisposeWith(disposables);
        });
    }
    
    private void OnOpened(object? sender, EventArgs e)
    {
        _windowHandle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        _taskbarProgressService.Initialize();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs windowClosingEventArgs)
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
    
    private bool _paused;

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
            ViewModel!.RipperSettings.SavePath = path;
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

    private async void PreviousHistoryPage(object? sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel!.DecrementHistoryPage();
            await LoadHistory();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load previous history page");
        }
    }

    // FIXME: NextHistoryPageExists() is not working as expected
    private async void NextHistoryPage(object? sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel!.IncrementHistoryPage();
            await LoadHistory();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load next history page");
        }
    }

    private async void UpdateCurrentHistoryPage(object? sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel!.RefreshHistoryPage();
            await LoadHistory();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to refresh history page");
        }
    }

    private async void UpdateCurrentHistoryPageWithFilter(object? send, RoutedEventArgs e)
    {
        try
        {
            var input = ViewModel!.HistoryFilterText.Trim();
            if (string.IsNullOrEmpty(input))
            {
                ViewModel.ClearHistoryFilter();
                await LoadHistory();
                return;
            }
        
            var filter = HistoryFilter.Parse(input);
            await LoadHistory(filter);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to apply history filter");
        }
    }
    
    private async void ClearHistoryFilter(object? sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel!.ClearHistoryFilter();
            await LoadHistory();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to clear history filter");
        }
    }

    private async Task LoadHistory(HistoryFilter? filter = null)
    {
        await ViewModel!.LoadHistory(filter);
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
                ViewModel.GuiSettings.NameWidth = dataGridLengthArgs.NewValue.Value.Value;
                break;
            case "HistoryUrl":
                ViewModel.GuiSettings.UrlWidth = dataGridLengthArgs.NewValue.Value.Value;
                break;
            case "HistoryDate":
                ViewModel.GuiSettings.DateWidth = dataGridLengthArgs.NewValue.Value.Value;
                break;
            case "HistoryCount":
                ViewModel.GuiSettings.CountWidth = dataGridLengthArgs.NewValue.Value.Value;
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
    
    private async void PlayPauseToggle()
    {
        try
        {
            if (_paused)
            {
                await Play();
                _paused = false;
            }
            else
            {
                await Pause();
                _paused = true;
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to toggle play/pause");
        }
    }

    private async Task Play()
    {
        var iconFound = this.TryGetResource("PauseButtonIcon", out var icon);
        if (iconFound)
        {
            PlayPauseButtonIcon.Data = (StreamGeometry)icon!;
        }

        bool playing;
        if (ViewModel is null)
        {
            playing = false;
        }
        else
        {
            playing = await ViewModel.Resume();
        }
        
        _paused = !playing;
    }

    private async Task Pause()
    {
        var iconFound = this.TryGetResource("PlayButtonIcon", out var icon);
        if (iconFound)
        {
            PlayPauseButtonIcon.Data = (StreamGeometry)icon!;
        }
        
        bool paused;
        if (ViewModel is null)
        {
            paused = false;
        }
        else
        {
            paused = await ViewModel.Pause();
        }
        
        _paused = paused;
    }
}