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
using Microsoft.Extensions.Logging;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.History;
using NicheImageRipper.Gui.ExtensionMethods;
using NicheImageRipper.Gui.Services;
using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.SiteModules.Modules.Booru;
using ReactiveUI;

namespace NicheImageRipper.Gui.ViewModels;

public abstract class MainWindowViewModelBase : ViewModelBase
{
    public IRipperClient RipperClient { get; }
    public IRipperSettings RipperSettings { get; }
    public IGuiSettings GuiSettings { get; }
    public ILogTextSource LogTextSource { get; }

    protected ILogger<MainWindowViewModelBase> Logger { get; }

    public abstract string Title { get; }
    public abstract bool IsThinClient { get; }

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

    /// <summary>Text of the page number box; applied by <see cref="GoToEnteredHistoryPage"/>.</summary>
    public string CurrentHistoryPageDisplay
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "1";

    public bool CanGoToPreviousHistoryPage
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool CanGoToNextHistoryPage
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string HistoryFilterText
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";

    public FilenameScheme[] FilenameSchemes { get; } = Enum.GetValues<FilenameScheme>();
    public UnzipProtocol[] UnzipProtocols { get; } = Enum.GetValues<UnzipProtocol>();

    public FilenameScheme FilenameScheme
    {
        get;
        set => SetSetting(ref field, value, v => RipperSettings.FilenameScheme = v);
    }

    public UnzipProtocol UnzipProtocol
    {
        get;
        set => SetSetting(ref field, value, v => RipperSettings.UnzipProtocol = v);
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
        set => SetSetting(ref field, value, v => RipperSettings.SavePath = v);
    } = "";

    public string UserAgent
    {
        get;
        set => SetSetting(ref field, value, v => RipperSettings.UserAgent = v);
    } = "";

    public bool SkipFailedDownloads
    {
        get;
        set => SetSetting(ref field, value, v => RipperSettings.SkipFailedDownloads = v);
    }

    public bool SaveUnfinishedUrls
    {
        get;
        set => SetSetting(ref field, value, v => RipperSettings.SaveUnfinishedUrls = v);
    }

    public bool RemoveDuplicates
    {
        get;
        set => SetSetting(ref field, value, v => RipperSettings.RemoveDuplicates = v);
    }

    public bool RunPostProcessScript
    {
        get;
        set => SetSetting(ref field, value, v => RipperSettings.RunPostProcessScript = v);
    }

    public string FlareSolverrUri
    {
        get;
        set => SetSetting(ref field, value, v => RipperSettings.FlareSolverrUri = v);
    } = "";

    public bool CloseFlareSolverrSession
    {
        get;
        set => SetSetting(ref field, value, v => RipperSettings.CloseFlareSolverrSession = v);
    }

    public string CSWebDriverUri
    {
        get;
        set => SetSetting(ref field, value, v => RipperSettings.CSWebDriverUri = v);
    } = "";

    public int SiteConfigCount
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
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
    public ReactiveCommand<CancellationToken, Unit> EditSiteConfigsCommand { get; }
    public ReactiveCommand<CancellationToken, Unit> CheckForUpdatesCommand { get; }
    public Interaction<ConfirmationViewModel, ConfirmationViewModel?> ShowConfirmationDialog { get; } = new();
    public Interaction<SiteConfigEditorViewModel, bool> ShowSiteConfigDialog { get; } = new();

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
        MaxRetriesDisplay = "";
        RetryDelayDisplay = "";
        RefreshSettings();

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
        EditSiteConfigsCommand = ReactiveCommand.CreateFromTask<CancellationToken>(EditSiteConfigs);
        CheckForUpdatesCommand = ReactiveCommand.CreateFromTask<CancellationToken>(CheckForUpdates);
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
        await RipperClient.InitializeAsync(cancellationToken);
        await RipperSettings.InitializeAsync(cancellationToken);
        await GuiSettings.InitializeAsync(cancellationToken);
        // Thin settings are only known once fetched from the backend, so re-sync what the settings tab shows
        Dispatcher.UIThread.Post(RefreshSettings);
        await RefreshHistory(cancellationToken);
    }

    /// <summary>
    ///     Called once the main window has opened. Full mode initializes immediately; the thin client overrides this
    ///     because it can only initialize after connecting to the backend.
    /// </summary>
    public virtual Task StartAsync(CancellationToken cancellationToken = default)
    {
        return InitializeAsync(cancellationToken);
    }

    /// <summary>
    ///     Re-reads every setting from <see cref="RipperSettings"/> so the settings tab matches the current config.
    /// </summary>
    public void RefreshSettings()
    {
        _refreshingSettings = true;
        try
        {
            UserAgent = RipperSettings.UserAgent;
            SavePath = RipperSettings.SavePath;
            FilenameScheme = RipperSettings.FilenameScheme;
            UnzipProtocol = RipperSettings.UnzipProtocol;
            RemoveDuplicates = RipperSettings.RemoveDuplicates;
            RunPostProcessScript = RipperSettings.RunPostProcessScript;
            SkipFailedDownloads = RipperSettings.SkipFailedDownloads;
            SaveUnfinishedUrls = RipperSettings.SaveUnfinishedUrls;
            MaxRetriesDisplay = RipperSettings.MaxRetries.ToString();
            RetryDelayDisplay = RipperSettings.RetryDelay.ToString();
            FlareSolverrUri = RipperSettings.FlareSolverrUri;
            CloseFlareSolverrSession = RipperSettings.CloseFlareSolverrSession;
            CSWebDriverUri = RipperSettings.CSWebDriverUri;
            SiteConfigCount = RipperSettings.Sites.Count;
        }
        finally
        {
            _refreshingSettings = false;
        }
    }

    private bool _refreshingSettings;

    /// <summary>
    ///     Sets a settings-tab property and pushes the new value to <see cref="RipperSettings"/>, unless the change
    ///     came from <see cref="RefreshSettings"/> (which is reading those values, not changing them).
    /// </summary>
    private void SetSetting<T>(ref T field, T value, Action<T> apply,
                               [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        this.RaiseAndSetIfChanged(ref field, value, propertyName);
        if (!_refreshingSettings)
        {
            apply(value);
        }
    }

    private async Task EditSiteConfigs(CancellationToken cancellationToken = default)
    {
        if (IsThinClient && !RipperClient.Connected)
        {
            Notify("Connect to the backend before editing site configs", "Site Configs", NotificationType.Warning);
            return;
        }

        var knownSiteNames = await RipperSettings.GetKnownSiteNames(cancellationToken);
        var editor = new SiteConfigEditorViewModel(RipperSettings.Sites, knownSiteNames);
        var saved = await ShowSiteConfigDialog.Handle(editor);
        if (!saved)
        {
            return;
        }

        RipperSettings.SetSites(editor.BuildSiteConfigs());
        SiteConfigCount = RipperSettings.Sites.Count;
        Notify("Site configs updated", "Site Configs", NotificationType.Success);
    }

    private async Task CheckForUpdates(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await RipperClient.CheckForUpdate(cancellationToken);
            switch (result.IsLatest)
            {
                case null:
                    Notify("Couldn't retrieve the latest version from GitHub", "Check For Updates",
                        NotificationType.Warning);
                    break;
                case true:
                    Notify($"You're on the latest version (v{result.CurrentVersion})", "Check For Updates",
                        NotificationType.Success);
                    break;
                case false:
                    Logger.LogInformation("Update available: {LatestVersion} (current {CurrentVersion})",
                        result.LatestVersion, result.CurrentVersion);
                    Notify($"Update available: v{result.LatestVersion} (current v{result.CurrentVersion})",
                        "Check For Updates", NotificationType.Information, 8000);
                    break;
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Failed to check for updates");
            Notify("Failed to check for updates", "Check For Updates", NotificationType.Error);
        }
    }

    private Task ToggleStateToRemote(CancellationToken cancellationToken = default)
    {
        return Active ? DisconnectFromRemote(cancellationToken) : ConnectToRemote(cancellationToken);
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
        return RipperClient.ClearCache(cancellationToken);
    }

    private void OnLogTextChanged(string text)
    {
        Dispatcher.UIThread.Post(() => { LogText = text; });
        LogTextChanged?.Invoke();
    }

    private Task DequeueUrls(CancellationToken cancellationToken = default)
    {
        return RipperClient.DequeueUrls(SelectedUrls, cancellationToken);
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
        Task.Run(() => QueueUrls(input, cancellationToken), cancellationToken);
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
                    rejectedUrls = await RipperClient.QueueUrls(string.Join("", urls), cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    Logger.LogWarning("Missing argument: <tags>");
                    return;
                }
            }
            else
            {
                rejectedUrls = await RipperClient.QueueUrls(input, cancellationToken);
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
                            var response = await ConfirmReripUrl(failedUrl.Url, cancellationToken);
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

                await RipperClient.RequeueUrls(rejectedUrls.WithRejectedUrls(urlsToRequeue), cancellationToken);
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
        {
            var temp = await ShowConfirmationDialog.Handle(confirmationViewModel);
            return temp;
        });
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
                    // TODO: Figure out how to remove dependency on SiteModules without duplicating code
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
        await RipperClient.ForceQueueUrl(url, cancellationToken);

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

    private HistoryFilter? _historyFilter;
    private int _historyLoadId;

    private int GetHistoryPageCount(int historyCount) => Math.Max(1, (historyCount + PageSize - 1) / PageSize);

    public Task PreviousHistoryPage(CancellationToken cancellationToken = default)
    {
        return CanGoToPreviousHistoryPage ? GoToHistoryPage(CurrentHistoryPage - 1, cancellationToken) : Task.CompletedTask;
    }

    public Task NextHistoryPage(CancellationToken cancellationToken = default)
    {
        return CanGoToNextHistoryPage ? GoToHistoryPage(CurrentHistoryPage + 1, cancellationToken) : Task.CompletedTask;
    }

    /// <summary>Goes to the page typed into the page number box, or restores the box if it isn't a number.</summary>
    public Task GoToEnteredHistoryPage(CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(CurrentHistoryPageDisplay.Trim(), out var page))
        {
            CurrentHistoryPageDisplay = CurrentHistoryPage.ToString();
            return Task.CompletedTask;
        }

        return GoToHistoryPage(page, cancellationToken);
    }

    /// <summary>Applies <see cref="HistoryFilterText"/> (empty clears the filter) and returns to the first page.</summary>
    public Task ApplyHistoryFilter(CancellationToken cancellationToken = default)
    {
        var input = HistoryFilterText.Trim();
        _historyFilter = string.IsNullOrEmpty(input) ? null : HistoryFilter.Parse(input);
        return GoToHistoryPage(1, cancellationToken);
    }

    public Task ClearHistoryFilter(CancellationToken cancellationToken = default)
    {
        HistoryFilterText = "";
        _historyFilter = null;
        return GoToHistoryPage(1, cancellationToken);
    }

    public Task RefreshHistory(CancellationToken cancellationToken = default)
    {
        return GoToHistoryPage(CurrentHistoryPage, cancellationToken);
    }

    /// <param name="page">One-based page number; clamped to the pages that exist.</param>
    private async Task GoToHistoryPage(int page, CancellationToken cancellationToken = default)
    {
        var loadId = Interlocked.Increment(ref _historyLoadId);
        var filter = _historyFilter;

        // Off the UI thread: the first history call opens the database, which can take a moment
        var (historyCount, loadedPage, history) = await Task.Run(async () =>
        {
            var count = await RipperClient.GetHistoryCount(cancellationToken);
            // The count ignores the filter, so only clamp the upper bound when unfiltered
            var targetPage = filter is null ? Math.Clamp(page, 1, GetHistoryPageCount(count)) : Math.Max(page, 1);
            var entries = await RipperClient.GetHistoryPage(targetPage - 1, PageSize, filter, cancellationToken);
            return (count, targetPage, entries);
        }, cancellationToken);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // A newer navigation started while this one was loading; let that one win
            if (loadId != Volatile.Read(ref _historyLoadId))
            {
                return;
            }

            CurrentHistoryPage = loadedPage;
            CurrentHistoryPageDisplay = loadedPage.ToString();
            CanGoToPreviousHistoryPage = loadedPage > 1;
            // With a filter the total is unknown, so assume another page exists while pages come back full
            CanGoToNextHistoryPage = filter is null
                ? loadedPage < GetHistoryPageCount(historyCount)
                : history.Count == PageSize;
            History.Update(history);
        });
        Logger.LogDebug("History page {Page} ({Count} entries, {Total} total)", loadedPage, history.Count, historyCount);
    }

    public void SetMaxRetries(int maxRetries)
    {
        // Core treats MaxRetries as the total number of attempts, so 0 would never rip anything
        if (maxRetries >= 1)
        {
            RipperSettings.MaxRetries = maxRetries;
        }

        MaxRetriesDisplay = RipperSettings.MaxRetries.ToString();
    }

    public void SetRetryDelay(int retryDelay)
    {
        if (retryDelay >= 0)
        {
            RipperSettings.RetryDelay = retryDelay;
        }

        RetryDelayDisplay = RipperSettings.RetryDelay.ToString();
    }

    public async Task<bool> Resume(CancellationToken cancellationToken = default)
    {
        var stateChanged = await RipperClient.Resume(cancellationToken);
        ProgressIsPaused = false;
        return stateChanged;
    }

    public async Task<bool> Pause(CancellationToken cancellationToken = default)
    {
        var stateChanged = await RipperClient.Pause(cancellationToken);
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