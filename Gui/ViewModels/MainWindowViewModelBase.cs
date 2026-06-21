using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.History;
using NicheImageRipper.Core.SiteParsing.HtmlParsers;
using Gui.ExtensionMethods;
using Gui.Services;
using Microsoft.Extensions.Logging;
using NicheImageRipper.Core.Enums;
using ReactiveUI;

namespace Gui.ViewModels;

public abstract class MainWindowViewModelBase : ViewModelBase
{
    public IRipperClient RipperClient { get; }
    public IRipperSettings RipperSettings { get; }
    public IGuiSettings GuiSettings { get; }
    public ILogTextSource LogTextSource { get; }

    protected ILogger<MainWindowViewModelBase> Logger { get; }

    public abstract string Title { get; }
    public abstract bool IsThinClient { get; }

    private int HistoryCount => RipperClient.GetHistoryCount().Result;

    private bool RipInProgress { get; set; }

    public string UrlInput
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";

    public string LogText
    {
        get;
        protected set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string UrlCountText
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "URLs in queue: 0";

    public int CurrentHistoryPage
    {
        get;
        private set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            CurrentHistoryPageDisplay = value.ToString();
        }
    } = 1;

    public string CurrentHistoryPageDisplay
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = "1";

    public string HistoryFilterText
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
            RipperSettings.FilenameScheme = (FilenameScheme)value;
        }
    }

    public int UnzipProtocolIndex
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            RipperSettings.UnzipProtocol = (UnzipProtocol)value;
        }
    }

    public string MaxRetriesDisplay
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string RetryDelayDisplay
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ulong ProgressCurrent
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ulong ProgressTotal
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool ProgressHasError
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool ProgressIsPaused
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsRipping
    {
        get;
        protected set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public int PageSize
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = 100;

    public double NameWidth
    {
        get;
        set
        {
            GuiSettings.NameWidth = value;
            this.RaiseAndSetIfChanged(ref field, value);
        }
    }

    public double UrlWidth
    {
        get;
        set
        {
            GuiSettings.UrlWidth = value;
            this.RaiseAndSetIfChanged(ref field, value);
        }
    }

    public double DateWidth
    {
        get;
        set
        {
            GuiSettings.DateWidth = value;
            this.RaiseAndSetIfChanged(ref field, value);
        }
    }

    public double CountWidth
    {
        get;
        set
        {
            GuiSettings.CountWidth = value;
            this.RaiseAndSetIfChanged(ref field, value);
        }
    }

    public string SavePath
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            RipperSettings.SavePath = value;
        }
    }

    public bool SkipFailedDownloads
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            RipperSettings.SkipFailedDownloads = value;
        }
    }

    public bool Active { get; set; }

    public ObservableCollection<string> UrlQueue { get; }
    public ObservableCollection<HistoryEntry> History { get; }

    public List<string> SelectedUrls
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    public string ConnectButtonDisplay
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "Connect";

    public ReactiveCommand<CancellationToken, Unit> RipCommand { get; }
    public ReactiveCommand<CancellationToken, Unit> ClearCacheCommand { get; }
    public ReactiveCommand<CancellationToken, Unit> DequeueUrlsCommand { get; }
    public ReactiveCommand<string, Unit> ReRipUrlCommand { get; }
    public ReactiveCommand<CancellationToken, Unit> ConnectCommand { get; }
    public ReactiveCommand<CancellationToken, Unit> SkipCurrentEntryCommand { get; }
    public Interaction<ConfirmationViewModel, ConfirmationViewModel?> ShowConfirmationDialog { get; } = new();

    public event Action? LogTextChanged;
    public event Action<int, int>? ProgressChanged;
    public event Action? ProgressErrored;
    public event Action<string, string, NotificationType, long>? OnNotification;

    protected MainWindowViewModelBase(IRipperClient ripperClient, IRipperSettings ripperSettings,
                                      IGuiSettings guiSettings, ILogTextSource logTextSource,
                                      ILogger<MainWindowViewModelBase> logger)
    {
        Logger = logger;
        RipperClient = ripperClient;

        RipperSettings = ripperSettings;
        MaxRetriesDisplay = RipperSettings.MaxRetries.ToString();
        RetryDelayDisplay = RipperSettings.RetryDelay.ToString();
        SavePath = RipperSettings.SavePath;
        SkipFailedDownloads = RipperSettings.SkipFailedDownloads;

        GuiSettings = guiSettings;
        NameWidth = GuiSettings.NameWidth;
        UrlWidth = GuiSettings.UrlWidth;
        DateWidth = GuiSettings.DateWidth;
        CountWidth = GuiSettings.CountWidth;

        LogTextSource = logTextSource;
        LogText = LogTextSource.CurrentText;
        LogTextSource.LogTextChanged += OnLogTextChanged;

        RipCommand = ReactiveCommand.CreateFromTask<CancellationToken>(QueueAndRip);
        ClearCacheCommand = ReactiveCommand.CreateFromTask<CancellationToken>(ClearCache);
        DequeueUrlsCommand = ReactiveCommand.CreateFromTask<CancellationToken>(DequeueUrls);
        ReRipUrlCommand = ReactiveCommand.CreateFromTask<string>(Rerip);
        ConnectCommand = ReactiveCommand.CreateFromTask<CancellationToken>(ToggleStateToRemote);
        SkipCurrentEntryCommand = ReactiveCommand.CreateFromTask<CancellationToken>(RipperClient.SkipCurrentEntry);
        UrlQueue = new ObservableCollection<string>([]);
        History = new ObservableCollection<HistoryEntry>([]);

        RipperClient.OnUrlQueueUpdated += OnUrlQueueUpdated;
        RipperClient.OnProgressChanged += OnProgressChanged;
    }

    private bool _initialized;

    protected virtual async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await RipperClient.InitializeAsync();
        await RipperSettings.InitializeAsync();
        await GuiSettings.InitializeAsync();
        var history = await GetHistoryPage(CurrentHistoryPage, PageSize);
        History.Update(history);
    }

    private Task ToggleStateToRemote(CancellationToken cancellationToken = default)
    {
        return Active ? DisconnectFromRemote() : ConnectToRemote();
    }

    protected virtual Task ConnectToRemote(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    protected virtual Task DisconnectFromRemote(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    private Task ClearCache(CancellationToken cancellationToken = default)
    {
        return RipperClient.ClearCache();
    }

    private Task<List<HistoryEntry>> GetHistoryPage(int start, int offset, HistoryFilter? filter = null) =>
        RipperClient.GetHistoryPage(start, offset, filter);

    private void OnLogTextChanged(string text)
    {
        Dispatcher.UIThread.Post(() => { LogText = text; });
        LogTextChanged?.Invoke();
    }

    private Task DequeueUrls(CancellationToken cancellationToken = default)
    {
        return RipperClient.DequeueUrls(SelectedUrls);
    }

    private Task QueueAndRip(CancellationToken cancellationToken = default)
    {
        var input = UrlInput;
        if (string.IsNullOrWhiteSpace(input) && RipperClient.UrlQueueCount == 0)
        {
            return Task.CompletedTask;
        }

        Logger.LogDebug("Queuing URL: {url}", input);

        UrlInput = "";
        Task.Run(() => QueueUrls(input));
        return Task.CompletedTask;
    }

    private async Task QueueUrls(string input, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(input))
        {
            var parts = SplitInput(input);
            RejectedUrlsInfo rejectedUrls;
            if (parts[0] == "booru")
            {
                try
                {
                    var urls = ExpandBooruInput(parts);
                    rejectedUrls = await RipperClient.QueueUrls(string.Join("", urls));
                }
                catch (InvalidOperationException)
                {
                    Logger.LogWarning("Missing argument: <tags>");
                    return;
                }
            }
            else
            {
                rejectedUrls = await RipperClient.QueueUrls(input);
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
                            Logger.LogInformation("URL already queued: {Url}", failedUrl.Url);
                            Notify($"URL already queued: {failedUrl.Url}", "Queue URL", NotificationType.Warning);
                            break;
                        case QueueFailureReason.NotSupported:
                            Logger.LogWarning("URL not supported: {Url}", failedUrl.Url);
                            Notify($"URL not supported: {failedUrl.Url}", "Queue URL", NotificationType.Error);
                            break;
                        case QueueFailureReason.PreviouslyProcessed:
                            Logger.LogInformation("Re-rip url? {Url}", failedUrl.Url);
                            var response = await ConfirmReripUrl(failedUrl.Url);
                            if (response)
                            {
                                Logger.LogDebug("Re-ripping URL: {Url}", failedUrl.Url);
                                urlsToRequeue.Add(failedUrl);
                            }
                            else
                            {
                                Logger.LogDebug("Skipping re-rip for URL: {Url}", failedUrl.Url);
                            }

                            break;
                        default:
                            throw new InvalidOperationException("Invalid QueueFailureReason: " + failedUrl.Reason);
                    }
                }

                await RipperClient.RequeueUrls(rejectedUrls.WithRejectedUrls(urlsToRequeue));
            }
        }

        Logger.LogDebug("URLS in queue: {count}", RipperClient.UrlQueueCount);

        if (RipInProgress)
        {
            return;
        }

        await Task.Run(() => Rip(cancellationToken), cancellationToken);
    }

    private async Task<bool> ConfirmReripUrl(string url, CancellationToken cancellationToken = default)
    {
        var confirmationViewModel = new ConfirmationViewModel
        {
            Message = $"Are you sure you want to re-rip this URL?\n{url}"
        };

        var result = await Dispatcher.UIThread.InvokeAsync(async () =>
        
            await ShowConfirmationDialog.Handle(confirmationViewModel)
        );
        return result?.Confirmed ?? false;
    }

    /// <summary>
    ///     Split input by spaces, but multiple spaces are treated as one
    /// </summary>
    /// <param name="input">String to split</param>
    /// <returns>List of split strings</returns>
    private static List<string> SplitInput(string input)
    {
        var parts = new List<string>();
        var currentPart = "";
        foreach (var c in input)
        {
            if (char.IsWhiteSpace(c))
            {
                if (currentPart == "")
                {
                    continue;
                }

                parts.Add(currentPart);
                currentPart = "";
            }
            else
            {
                currentPart += c;
            }
        }

        if (currentPart != "")
        {
            parts.Add(currentPart);
        }

        return parts;
    }

    private static List<string> ExpandBooruInput(List<string> parts)
    {
        if (parts.Count < 2)
        {
            throw new InvalidOperationException("Missing argument: <tags>");
        }

        // This is prob unintuitive
        // Basically, any booru-like URL (i.e. starts with https:// and contains tags=) is converted to the global booru URL
        //      such that the ripper will pull from all supported boorus
        // Any other parts are treated as tags for a single booru search, but if a url is encountered, the tags
        //      are queued first, then the url is queued, and subsequent tags are treated as a different search
        // This process repeats until all input is consumed
        // Example input:
        // booru cute tall https://example.booru.com/post?tags=cat+animal funny
        // Results in three searches being queued:
        // 1. cute, tall
        // 2. cat, animal
        // 3. funny
        var urls = new List<string>();
        var tags = new List<string>();
        foreach (var part in parts.Skip(1))
        {
            if (part.StartsWith("https://"))
            {
                if (tags.Count > 0)
                {
                    var tagsString = string.Join("+", tags);
                    var url = "https://booru.com/post?tags=" + tagsString;
                    urls.Add(url);
                    tags.Clear();
                }

                {
                    // May throw an exception, if the input is not a booru-like URL
                    var tagsString = BooruParser.ExtractTagsFromUrl(part);
                    if (tagsString.EndsWith('+'))
                    {
                        tagsString = tagsString[..^1]; // Remove trailing +
                    }

                    var url = "https://booru.com/post?" + tagsString;
                    urls.Add(url);
                }
            }
            else
            {
                tags.Add(part);
            }
        }

        if (tags.Count > 0)
        {
            var tagsString = string.Join("+", tags);
            var url = "https://booru.com/post?tags=" + tagsString;
            urls.Add(url);
        }

        return urls;
    }

    private async Task Rerip(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            Logger.LogWarning("Cannot re-rip an empty URL.");
            return;
        }

        Logger.LogDebug("Re-ripping URL: {url}", url);
        await RipperClient.ForceQueueUrl(url);

        Logger.LogDebug("URLS in queue: {count}", RipperClient.UrlQueueCount);

        if (RipInProgress)
        {
            return;
        }

        await Task.Run(() => Rip(cancellationToken), cancellationToken);
    }

    private async Task Rip(CancellationToken cancellationToken = default)
    {
        RipInProgress = true;
        try
        {
            await RipperClient.Rip(cancellationToken);
            ClearProgressState();
        }
        catch (Exception e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                Logger.LogError(e, "Error occurred while ripping");
                SetProgressError();
            });
        }
        finally
        {
            RipInProgress = false;
        }
    }

    protected void OnUrlQueueUpdated()
    {
        Dispatcher.UIThread.Post(async void () =>
        {
            try
            {
                var queueIter = await RipperClient.GetUrlQueue();
                var queue = queueIter.ToList();
                UrlQueue.Update(queue);
                UrlCountText = $"URLs in queue: {queue.Count}";
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error occurred while fetching url queue");
            }
        });
    }

    private void OnProgressChanged(int current, int total)
    {
        // ProgressCurrent = (ulong)current;
        // ProgressTotal = (ulong)total;
        ProgressChanged?.Invoke(current, total);
    }

    public void LoadUnfinishedUrls(string path)
    {
        RipperClient.LoadUrlFile(path);
    }

    public void SaveData()
    {
        RipperClient.SaveData();
    }

    public void Cleanup()
    {
        RipperClient.Dispose();
    }

    public void DecrementHistoryPage()
    {
        if (CurrentHistoryPage > 1)
        {
            CurrentHistoryPage--;
        }
    }

    public void IncrementHistoryPage()
    {
        if (NextHistoryPageExists())
        {
            CurrentHistoryPage++;
        }
    }

    public bool NextHistoryPageExists()
    {
        Logger.LogDebug("CurrentHistoryPage: {CurrentHistoryPage}, PageSize: {PageSize}, HistoryCount: {HistoryCount}",
            CurrentHistoryPageDisplay, PageSize, HistoryCount);
        return HistoryCount - (CurrentHistoryPage * PageSize) > PageSize;
    }

    public void RefreshHistoryPage()
    {
        if (CurrentHistoryPage < 1)
        {
            CurrentHistoryPage = 1;
        }
        else if (CurrentHistoryPage > HistoryCount / PageSize)
        {
            CurrentHistoryPage = HistoryCount / PageSize;
        }
    }

    public void ClearHistoryFilter()
    {
        HistoryFilterText = "";
    }

    public async Task LoadHistory(HistoryFilter? filter = null, CancellationToken cancellationToken = default)
    {
        var history = await GetHistoryPage(CurrentHistoryPage - 1, PageSize, filter);
        Logger.LogDebug("History[{Count}]: {@History}", history.Count, history.Count == 0 ? "None" : history[0]);
        History.Update(history);
    }

    public void SetMaxRetries(int maxRetries)
    {
        if (maxRetries <= 0)
        {
            MaxRetriesDisplay = RipperSettings.MaxRetries.ToString();
        }
        else
        {
            RipperSettings.MaxRetries = maxRetries;
        }
    }

    public void SetRetryDelay(int result)
    {
        if (result <= 0)
        {
            RetryDelayDisplay = RipperSettings.RetryDelay.ToString();
        }
        else
        {
            RipperSettings.RetryDelay = result;
        }
    }

    public async Task<bool> Resume(CancellationToken cancellationToken = default)
    {
        var stateChanged = await RipperClient.Resume();
        ProgressIsPaused = false;
        return stateChanged;
    }

    public async Task<bool> Pause(CancellationToken cancellationToken = default)
    {
        var stateChanged = await RipperClient.Pause();
        ProgressIsPaused = true;
        return stateChanged;
    }

    private void ClearProgressState()
    {
        ProgressCurrent = 0;
        ProgressTotal = 0;
        ProgressHasError = false;
        ProgressIsPaused = false;
    }

    private void SetProgressError()
    {
        //ProgressHasError = true;
        ProgressErrored?.Invoke();
    }

    private void Notify(string message, string title = "Snackbar", NotificationType type = NotificationType.Information,
                        long expirationMs = 3000)
    {
        OnNotification?.Invoke(message, title, type, expirationMs);
    }
}