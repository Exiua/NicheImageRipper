using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Common.Utility;
using Core;
using Core.DataStructures;
using Core.Enums;
using Core.History;
using Core.SiteParsing.HtmlParsers;
using Gui.Utility;
using Gui.Models;
using Gui.Views;
using ReactiveUI;
using Serilog;

namespace Gui.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly NicheImageRipper _ripper;
    private readonly ITaskbarProgressService? _progressService = GetTaskbarProgressService(); //TODO: Implement usage
    
    private static readonly Version Version = new(1, 0, 0);
    private static GuiConfig Config => (GuiConfig) Core.Configuration.Config.Instance;

    public static string Title => $"Gui v{Version} - Core v{NicheImageRipper.Version}";

    internal MainWindow MainWindow { get; set; } = null!;

    private bool _ripInProgress;
    private int _currentHistoryPage;

    public int HistoryCount => NicheImageRipper.GetHistoryCount();
    public int PageSize { get; set; } = 100;
    public ObservableCollection<string> UrlQueue { get; }
    public ObservableCollection<HistoryEntry> History { get; }

    public List<string> SelectedUrls { get; set; } = [];

    public string SavePath
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            NicheImageRipper.SavePath = value;
        }
    } = NicheImageRipper.SavePath;

    public string UrlInput
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";

    public int FilenameSchemeIndex
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            NicheImageRipper.FilenameScheme = (FilenameScheme)value;
        }
    }

    public int UnzipProtocolIndex
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            NicheImageRipper.UnzipProtocol = (UnzipProtocol)value;
        }
    }

    public string LogText
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";

    public string CurrentHistoryPageDisplay
    {
        get;
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

            this.RaiseAndSetIfChanged(ref field, result.ToString());
        }
    } = "1";

    public string MaxRetriesDisplay
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = NicheImageRipper.MaxRetries.ToString();

    public string RetryDelayDisplay
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = NicheImageRipper.RetryDelay.ToString();

    public bool SkipFailedDownloads
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            NicheImageRipper.SkipFailedDownloads = value;
        }
    }

    public double NameWidth
    {
        get;
        set
        {
            Config.HistoryColumnWidths.NameWidth = value;
            this.RaiseAndSetIfChanged(ref field, value);
        }
    } = Config.HistoryColumnWidths.NameWidth;

    public double UrlWidth
    {
        get;
        set
        {
            Config.HistoryColumnWidths.UrlWidth = value;
            this.RaiseAndSetIfChanged(ref field, value);
        }
    } = Config.HistoryColumnWidths.UrlWidth;

    public double DateWidth
    {
        get;
        set
        {
            Config.HistoryColumnWidths.DateWidth = value;
            this.RaiseAndSetIfChanged(ref field, value);
        }
    } = Config.HistoryColumnWidths.DateWidth;

    public double CountWidth
    {
        get;
        set
        {
            Config.HistoryColumnWidths.CountWidth = value;
            this.RaiseAndSetIfChanged(ref field, value);
        }
    } = Config.HistoryColumnWidths.CountWidth;

    public string HistoryFilterText
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";

    public string UrlCountText
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "URLs in queue: 0";

    public ReactiveCommand<Unit, Unit> RipCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearCacheCommand { get; }
    public ReactiveCommand<Unit, Unit> DequeueUrlsCommand { get; }
    public ReactiveCommand<string, Unit> ReRipUrlCommand { get; }
    public Interaction<ConfirmationViewModel, ConfirmationViewModel?> ShowConfirmationDialog { get; } = new();

    static MainWindowViewModel()
    {
        // FIXME: There is probably a cleaner way to structure this helper function
        GuiUtility.ExtractTagsFromUrlFunc = BooruParser.ExtractTagsFromUrl;
    }
    
    public MainWindowViewModel()
    {
        _ripper = new NicheImageRipper();
        RipCommand = ReactiveCommand.CreateRunInBackground(QueueAndRip);
        ClearCacheCommand = ReactiveCommand.Create(ClearCache);
        DequeueUrlsCommand = ReactiveCommand.Create(DequeueUrls);
        ReRipUrlCommand = ReactiveCommand.Create<string>(Rerip);
        UrlQueue = new ObservableCollection<string>(_ripper.UrlQueue);
        var history = NicheImageRipper.GetHistoryPage(_currentHistoryPage, PageSize);
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

    public bool Play()
    {
        return _ripper.Resume();
    }

    public bool Pause()
    {
        return _ripper.Pause();
    }

    private void OnUrlQueueUpdated()
    {
        Dispatcher.UIThread.Post(() =>
        {
            UrlQueue.Update(_ripper.UrlQueue);
            UrlCountText = $"URLs in queue: {_ripper.UrlQueue.Count}";
        });
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

    private void Rerip(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            Log.Warning("Cannot re-rip an empty URL.");
            return;
        }
        
        Log.Debug("Re-ripping URL: {url}", url);
        _ripper.ForceQueueUrl(url);
        
        Log.Debug("URLS in queue: {count}", _ripper.UrlQueue.Count);

        if (_ripInProgress)
        {
            return;
        }

        Task.Run(Rip);
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
        if (!string.IsNullOrWhiteSpace(input))
        {
            var parts = GuiUtility.SplitInput(input);
            RejectedUrlsInfo rejectedUrls;
            if (parts[0] == "booru")
            {
                try
                {
                    var urls = GuiUtility.ExpandBooruInput(parts);
                    rejectedUrls = _ripper.QueueUrls(string.Join("", urls));
                }
                catch (InvalidOperationException)
                {
                    Log.Warning("Missing argument: <tags>");
                    return;
                }
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

    public void LoadHistory(HistoryFilter? filter = null)
    {
        var history = NicheImageRipper.GetHistoryPage(_currentHistoryPage - 1, PageSize, filter);
        Log.Debug("History[{Count}]: {@History}", history.Count, history.Count == 0 ? "None" : history[0]);
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

    public void ClearHistoryFilter()
    {
        HistoryFilterText = "";
    }
}