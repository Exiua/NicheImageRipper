using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Core;
using Core.DataStructures;
using Core.Enums;
using CoreGui.Models;
using CoreGui.Utility;
using CoreGui.Views;
using ReactiveUI;
using Serilog;

namespace CoreGui.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly NicheImageRipper _ripper;
    private readonly ITaskbarProgressService? _progressService = GetTaskbarProgressService(); //TODO: Implement usage

    private static GuiConfig Config => (GuiConfig) Core.Configuration.Config.Instance;

    internal MainWindow MainWindow { get; set; } = null!;

    private bool _ripInProgress;
    private string _urlInput = "";
    private string _savePath = NicheImageRipper.SavePath;
    private int _filenameSchemeIndex;
    private int _unzipProtocolIndex;
    private string _logText = "";
    private string _currentHistoryPageDisplay = "1";
    private int _currentHistoryPage = 1;
    private string _maxRetriesDisplay = NicheImageRipper.MaxRetries.ToString();
    private string _retryDelayDisplay = NicheImageRipper.RetryDelay.ToString();
    private double _nameWidth = Config.HistoryColumnWidths.NameWidth;
    private double _urlWidth = Config.HistoryColumnWidths.UrlWidth;
    private double _dateWidth = Config.HistoryColumnWidths.DateWidth;
    private double _countWidth = Config.HistoryColumnWidths.CountWidth;

    public int HistoryCount => NicheImageRipper.GetHistoryCount();
    public int PageSize { get; set; } = 100;
    public ObservableCollection<string> UrlQueue { get; }
    public ObservableCollection<HistoryEntry> History { get; }

    public List<string> SelectedUrls { get; set; } = [];

    public string SavePath
    {
        get => _savePath;
        set
        {
            this.RaiseAndSetIfChanged(ref _savePath, value);
            NicheImageRipper.SavePath = value;
        }
    }

    public string UrlInput
    {
        get => _urlInput;
        set => this.RaiseAndSetIfChanged(ref _urlInput, value);
    }

    public int FilenameSchemeIndex
    {
        get => _filenameSchemeIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _filenameSchemeIndex, value);
            NicheImageRipper.FilenameScheme = (FilenameScheme)value;
        }
    }

    public int UnzipProtocolIndex
    {
        get => _unzipProtocolIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _unzipProtocolIndex, value);
            NicheImageRipper.UnzipProtocol = (UnzipProtocol)value;
        }
    }

    public string LogText
    {
        get => _logText;
        set => this.RaiseAndSetIfChanged(ref _logText, value);
    }

    public string CurrentHistoryPageDisplay
    {
        get => _currentHistoryPageDisplay;
        set
        {
            if (int.TryParse(value, out var result))
            {
                _currentHistoryPage = result;
            }
            else
            {
                result = -1;
            }

            this.RaiseAndSetIfChanged(ref _currentHistoryPageDisplay, result.ToString());
        }
    }

    public string MaxRetriesDisplay
    {
        get => _maxRetriesDisplay;
        set => this.RaiseAndSetIfChanged(ref _maxRetriesDisplay, value);
    }

    public string RetryDelayDisplay
    {
        get => _retryDelayDisplay;
        set => this.RaiseAndSetIfChanged(ref _retryDelayDisplay, value);
    }

    public double NameWidth
    {
        get => _nameWidth;
        set
        {
            Config.HistoryColumnWidths.NameWidth = value;
            this.RaiseAndSetIfChanged(ref _nameWidth, value);
        }
    }

    public double UrlWidth
    {
        get => _urlWidth;
        set
        {
            Config.HistoryColumnWidths.UrlWidth = value;
            this.RaiseAndSetIfChanged(ref _urlWidth, value);
        }
    }

    public double DateWidth
    {
        get => _dateWidth;
        set
        {
            Config.HistoryColumnWidths.DateWidth = value;
            this.RaiseAndSetIfChanged(ref _dateWidth, value);
        }
    }

    public double CountWidth
    {
        get => _countWidth;
        set
        {
            Config.HistoryColumnWidths.CountWidth = value;
            this.RaiseAndSetIfChanged(ref _countWidth, value);
        }
    }

    public ReactiveCommand<Unit, Unit> RipCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearCacheCommand { get; }
    public ReactiveCommand<Unit, Unit> DequeueUrlsCommand { get; }
    public Interaction<ConfirmationViewModel, ConfirmationViewModel?> ShowConfirmationDialog { get; } = new();

    public MainWindowViewModel()
    {
        _ripper = new NicheImageRipper();
        RipCommand = ReactiveCommand.CreateRunInBackground(QueueAndRip);
        ClearCacheCommand = ReactiveCommand.Create(ClearCache);
        DequeueUrlsCommand = ReactiveCommand.Create(DequeueUrls);
        UrlQueue = new ObservableCollection<string>(_ripper.UrlQueue);
        var history = NicheImageRipper.GetHistoryPage(1, PageSize);
        History = new ObservableCollection<HistoryEntry>(history);

        _ripper.OnUrlQueueUpdated += OnUrlQueueUpdated;
        _ripper.OnProgressChanged += OnProgressChanged;
    }
    
    private static ITaskbarProgressService? GetTaskbarProgressService()
    {
        #if WINDOWS
        return new WindowsTaskbarProgressService();
        #else
        return null;
        #endif
    }

    public void DecrementHistoryPage()
    {
        if (_currentHistoryPage > 1)
        {
            _currentHistoryPage--;
            CurrentHistoryPageDisplay = _currentHistoryPage.ToString();
        }
    }

    public void IncrementHistoryPage()
    {
        if (NextHistoryPageExists())
        {
            _currentHistoryPage++;
            CurrentHistoryPageDisplay = _currentHistoryPage.ToString();
        }
    }

    public void RefreshHistoryPage()
    {
        if (_currentHistoryPage < 1)
        {
            _currentHistoryPage = 1;
        }
        else if (_currentHistoryPage > HistoryCount / PageSize)
        {
            _currentHistoryPage = HistoryCount / PageSize;
        }

        CurrentHistoryPageDisplay = _currentHistoryPage.ToString();
    }

    private static void ClearCache()
    {
        NicheImageRipper.ClearCache();
    }

    private void OnUrlQueueUpdated()
    {
        Dispatcher.UIThread.Post(() => UrlQueue.Update(_ripper.UrlQueue));
    }

    private void OnProgressChanged(int current, int total)
    {
        if (_progressService is not null)
        {
            var windowHandle = GetWindowHandle();
            if (windowHandle == IntPtr.Zero)
            {
                Log.Warning("Failed to get window handle for progress update.");
                return;
            }
            
            _progressService.SetProgress(windowHandle, (ulong)current, (ulong)total);
        }
    }
    
    private IntPtr GetWindowHandle()
    {
        var handle = TopLevel.GetTopLevel(MainWindow)?.TryGetPlatformHandle()?.Handle;
        return handle ?? IntPtr.Zero;
    }
    
    private void DequeueUrls()
    {
        _ripper.DequeueUrls(SelectedUrls);
    }

    private void QueueAndRip()
    {
        var input = UrlInput;
        if (string.IsNullOrWhiteSpace(input) && _ripper.UrlQueue.Count == 0)
        {
            return;
        }

        Log.Debug("Queuing URL: {url}", input);

        UrlInput = "";
        Task.Run(() => QueueUrls(input));
    }

    private async Task QueueUrls(string input)
    {
        var parts = input.Split(" ");
        RejectedUrlsInfo rejectedUrls;
        if (parts[0] == "booru")
        {
            if (parts.Length < 2)
            {
                Log.Warning("Missing argument: <tags>");
                return;
            }

            var tags = parts[1];
            var url = "https://booru.com/post?tags=" + tags;
            rejectedUrls = _ripper.QueueUrls(url);
        }
        else
        {
            rejectedUrls = _ripper.QueueUrls(input);
        }
        
        if (rejectedUrls.Count != 0)
        {
            var urlsToRequeue = new List<RejectedUrlInfo>(rejectedUrls.Count);
            foreach (var failedUrl in rejectedUrls.Urls)
            {
                switch (failedUrl.Reason)
                {
                    case QueueFailureReason.None:
                        break;
                    case QueueFailureReason.AlreadyQueued:
                        Log.Information("URL already queued: {Url}", failedUrl.Url);
                        break;
                    case QueueFailureReason.NotSupported:
                        Log.Warning("URL not supported: {Url}", failedUrl.Url);
                        break;
                    case QueueFailureReason.PreviouslyProcessed:
                        Log.Information("Re-rip url? {Url}", failedUrl.Url);
                        var response = await ConfirmReripUrl(failedUrl.Url);
                        if (response)
                        {
                            Log.Debug("Re-ripping URL: {Url}", failedUrl.Url);
                            urlsToRequeue.Add(failedUrl);
                        }
                        else
                        {
                            Log.Debug("Skipping re-rip for URL: {Url}", failedUrl.Url);
                        }

                        break;
                    default:
                        throw new InvalidOperationException("Invalid QueueFailureReason: " + failedUrl.Reason);
                }
            }

            _ripper.RequeueUrls(rejectedUrls.WithRejectedUrls(urlsToRequeue));
        }

        Log.Debug("URLS in queue: {count}", _ripper.UrlQueue.Count);

        if (_ripInProgress)
        {
            return;
        }

        await Task.Run(Rip);
    }

    private async Task<bool> ConfirmReripUrl(string url)
    {
        var confirmationViewModel = new ConfirmationViewModel
        {
            Message = $"Are you sure you want to re-rip this URL?\n{url}"
        };
        
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var windows = new ConfirmationWindow
            {
                DataContext = confirmationViewModel
            };
            await windows.ShowDialog(MainWindow);
        });
        
        return confirmationViewModel.Confirmed;
    }

    private async Task Rip()
    {
        _ripInProgress = true;
        try
        {
            await _ripper.Rip();
        
            if (_progressService is not null)
            {
                var windowHandle = GetWindowHandle();
                if (windowHandle != IntPtr.Zero)
                {
                    _progressService.ClearProgress(windowHandle);
                }
            }
        }
        catch (Exception e)
        {
            Dispatcher.UIThread.Post(() => { Log.Error(e, "Error occurred while ripping"); });
            if (_progressService is not null)
            {
                var windowHandle = GetWindowHandle();
                if (windowHandle != IntPtr.Zero)
                {
                    _progressService.SetError(windowHandle);
                }
            }
        }
        finally
        {
            _ripInProgress = false;
        }
    }

    internal void LoadUnfinishedUrls(string path)
    {
        _ripper.LoadUrlFile(path);
    }

    public void LoadHistory()
    {
        var history = NicheImageRipper.GetHistoryPage(_currentHistoryPage - 1, PageSize);
        Log.Debug("History[{Count}]: {@History}", history.Count, history[0]);
        History.Update(history);
    }

    public bool NextHistoryPageExists()
    {
        Log.Debug("CurrentHistoryPage: {CurrentHistoryPage}, PageSize: {PageSize}, HistoryCount: {HistoryCount}",
            CurrentHistoryPageDisplay, PageSize, HistoryCount);
        return HistoryCount - (_currentHistoryPage * PageSize) > PageSize;
    }

    public void SaveData()
    {
        _ripper.SaveData();
    }

    public void Cleanup()
    {
        _ripper.Dispose();
    }

    public void SetMaxRetries(int maxRetries)
    {
        if (maxRetries == -1)
        {
            MaxRetriesDisplay = NicheImageRipper.MaxRetries.ToString();
        }
        else
        {
            NicheImageRipper.MaxRetries = maxRetries;
        }
    }

    public void SetRetryDelay(int result)
    {
        if (result == -1)
        {
            RetryDelayDisplay = NicheImageRipper.RetryDelay.ToString();
        }
        else
        {
            NicheImageRipper.RetryDelay = result;
        }
    }
}