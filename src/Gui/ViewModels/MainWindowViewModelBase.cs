using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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
using NicheImageRipper.Gui.Services.Shared;
using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Sdk.Enums;
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

    public int QueueCount
    {
        get;
        private set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(HasQueuedUrls));
        }
    }

    public bool HasQueuedUrls => QueueCount > 0;

    public string? CurrentUrl
    {
        get;
        private set
        {
            var startedNewUrl = value is not null && value != field;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(HasCurrentUrl));
            if (startedNewUrl)
            {
                // Progress events belong to the previous URL until the new rip reports its own
                ProgressCurrent = 0;
                ProgressTotal = 0;
            }

            UpdateProgressDisplay();
        }
    }

    public bool HasCurrentUrl => CurrentUrl is not null;

    public bool HasSelectedUrls
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public IReadOnlyList<string> ThemeOptions => ThemeManager.Themes;

    public string SelectedTheme
    {
        get;
        set
        {
            value ??= ThemeManager.System; // ListBox briefly clears its selection while re-templating
            SetSetting(ref field, value, v => GuiSettings.Theme = v);
            ThemeManager.ApplyTheme(value);
        }
    } = ThemeManager.System;

    public IReadOnlyList<AccentOption> AccentOptions => AccentOption.All;

    public AccentOption SelectedAccent
    {
        get;
        set
        {
            value ??= AccentOption.All[0];
            SetSetting(ref field, value, v => GuiSettings.AccentColor = v.Hex ?? "");
            ThemeManager.ApplyAccent(value.Hex);
        }
    } = AccentOption.All[0];

    public double ProgressPercent
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsProgressIndeterminate
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string ProgressText
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";

    /// <summary>The strip keeps its space when idle so the layout doesn't jump when a rip starts.</summary>
    public double ProgressStripOpacity
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string CoreVersionText
    {
        get;
        protected set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";

    public bool ShowLogTimestampAndLevel
    {
        get;
        set
        {
            SetSetting(ref field, value, v => GuiSettings.ShowLogTimestampAndLevel = v);
            LogTextSource.ShowTimestampAndLevel = value;
        }
    }

    public bool ShowDebugLogs
    {
        get;
        set
        {
            SetSetting(ref field, value, v => GuiSettings.ShowDebugLogs = v);
            LogTextSource.ShowDebug = value;
        }
    }

    public bool WrapLogLines
    {
        get;
        set => SetSetting(ref field, value, v => GuiSettings.WrapLogLines = v);
    }

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

    public ReRipBehavior[] ReRipBehaviors { get; } = Enum.GetValues<ReRipBehavior>();

    public ReRipBehavior ReRipBehavior
    {
        get;
        set => SetSetting(ref field, value, v => RipperSettings.ReRipBehavior = v);
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

    /// <summary>Thin client: connected to the backend. Always false in full mode.</summary>
    public bool Active
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(CanRip));
            this.RaisePropertyChanged(nameof(ConnectionText));
        }
    }

    public bool CanRip => !IsThinClient || Active;
    public string ConnectionText => Active ? "Connected" : "Disconnected";

    public ObservableCollection<string> UrlQueue { get; }
    public ObservableCollection<HistoryEntry> History { get; }

    /// <summary>Kept in sync by the queue ListBox (bound to its SelectedItems).</summary>
    public ObservableCollection<string> SelectedUrls { get; } = [];

    public string ConnectButtonDisplay
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "Connect";

    public ReactiveCommand<CancellationToken, Unit> RipCommand { get; }
    public ReactiveCommand<CancellationToken, Unit> ClearCacheCommand { get; }
    public ReactiveCommand<CancellationToken, Unit> DequeueUrlsCommand { get; }
    public ReactiveCommand<CancellationToken, Unit> ClearQueueCommand { get; }
    public ReactiveCommand<CancellationToken, Unit> SaveQueueAndClearCommand { get; }
    public ReactiveCommand<CancellationToken, Unit> LoadUnfinishedUrlsCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearLogCommand { get; }
    public ReactiveCommand<string, Unit> ReRipUrlCommand { get; }
    public ReactiveCommand<CancellationToken, Unit> ConnectCommand { get; }
    public ReactiveCommand<CancellationToken, Unit> SkipCurrentEntryCommand { get; }
    public ReactiveCommand<CancellationToken, Unit> EditSiteConfigsCommand { get; }
    public ReactiveCommand<CancellationToken, Unit> CheckForUpdatesCommand { get; }
    public Interaction<ConfirmationViewModel, ConfirmationViewModel?> ShowConfirmationDialog { get; } = new();
    public Interaction<SiteConfigEditorViewModel, bool> ShowSiteConfigDialog { get; } = new();
    /// <summary>Returns the full path of the URL list to load, or null if cancelled.</summary>
    public Interaction<UnfinishedUrlsViewModel, string?> ShowUnfinishedUrlsDialog { get; } = new();

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
        SelectedTheme = ThemeManager.Themes.FirstOrDefault(t => string.Equals(t, GuiSettings.Theme,
            StringComparison.OrdinalIgnoreCase)) ?? ThemeManager.System;
        SelectedAccent = AccentOption.All.FirstOrDefault(a => string.Equals(a.Hex ?? "", GuiSettings.AccentColor,
            StringComparison.OrdinalIgnoreCase)) ?? AccentOption.All[0];
        ShowLogTimestampAndLevel = GuiSettings.ShowLogTimestampAndLevel;
        ShowDebugLogs = GuiSettings.ShowDebugLogs;
        WrapLogLines = GuiSettings.WrapLogLines;

        RipCommand = ReactiveCommand.CreateFromTask<CancellationToken>(QueueAndRip);
        ClearCacheCommand = ReactiveCommand.CreateFromTask<CancellationToken>(ClearCache);
        SelectedUrls.CollectionChanged += (_, _) => HasSelectedUrls = SelectedUrls.Count > 0;
        DequeueUrlsCommand = ReactiveCommand.CreateFromTask<CancellationToken>(DequeueUrls,
            this.WhenAnyValue(x => x.HasSelectedUrls));
        ClearQueueCommand = ReactiveCommand.CreateFromTask<CancellationToken>(ClearQueue,
            this.WhenAnyValue(x => x.QueueCount, count => count > 0));
        SaveQueueAndClearCommand = ReactiveCommand.CreateFromTask<CancellationToken>(SaveQueueAndClear,
            this.WhenAnyValue(x => x.QueueCount, count => count > 0));
        ClearLogCommand = ReactiveCommand.Create(() => LogTextSource.Clear());
        LoadUnfinishedUrlsCommand = ReactiveCommand.CreateFromTask<CancellationToken>(LoadUnfinishedUrls);
        ReRipUrlCommand = ReactiveCommand.CreateFromTask<string>(Rerip);
        ConnectCommand = ReactiveCommand.CreateFromTask<CancellationToken>(ToggleStateToRemote);
        SkipCurrentEntryCommand = ReactiveCommand.CreateFromTask<CancellationToken>(RipperClient.SkipCurrentEntry);
        EditSiteConfigsCommand = ReactiveCommand.CreateFromTask<CancellationToken>(EditSiteConfigs);
        CheckForUpdatesCommand = ReactiveCommand.CreateFromTask<CancellationToken>(CheckForUpdates);
        UrlQueue = new ObservableCollection<string>([]);
        History = new ObservableCollection<HistoryEntry>([]);

        RipperClient.OnUrlQueueUpdated += OnUrlQueueUpdated;
        RipperClient.OnProgressChanged += OnProgressChanged;
        RipperClient.OnRipStopped += OnRipStopped;
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
        var coreVersion = await RipperClient.GetCoreVersion(cancellationToken);
        Dispatcher.UIThread.Post(() => CoreVersionText = $"Core v{coreVersion}");
        OnUrlQueueUpdated();
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
            ReRipBehavior = RipperSettings.ReRipBehavior;
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
        if (IsThinClient && !Active)
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

    /// <summary>Clears what the window shows about a backend's queue, e.g. after disconnecting from it.</summary>
    protected void ClearRemoteQueueDisplay()
    {
        Dispatcher.UIThread.Post(() =>
        {
            UrlQueue.Clear();
            QueueCount = 0;
            CurrentUrl = null;
        });
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

    private Task DequeueUrls(CancellationToken cancellationToken = default)
    {
        // Copy: the ListBox edits SelectedUrls as the removed items disappear from the queue
        return RipperClient.DequeueUrls(SelectedUrls.ToList(), cancellationToken);
    }

    private async Task ClearQueue(CancellationToken cancellationToken = default)
    {
        var urls = UrlQueue.ToList();
        if (urls.Count == 0)
        {
            return;
        }

        var message = $"Remove all {urls.Count} URL(s) from the queue?";
        if (CurrentUrl is not null)
        {
            message += "\nThe URL currently being ripped will finish, but won't be retried.";
        }

        var confirmation = await ShowConfirmationDialog.Handle(new ConfirmationViewModel { Message = message });
        if (confirmation?.Confirmed != true)
        {
            return;
        }

        await RipperClient.DequeueUrls(urls, cancellationToken);
    }

    private async Task SaveQueueAndClear(CancellationToken cancellationToken = default)
    {
        var count = UrlQueue.Count;
        if (count == 0)
        {
            return;
        }

        var message = $"Save {count} URL(s) to a file and clear the queue?\n" +
                      "Use Load unfinished… to pick them up again later.";
        if (CurrentUrl is not null)
        {
            message += "\nThe URL currently being ripped will finish, and is included in the file in case it doesn't.";
        }

        var confirmation = await ShowConfirmationDialog.Handle(new ConfirmationViewModel { Message = message });
        if (confirmation?.Confirmed != true)
        {
            return;
        }

        try
        {
            var path = await RipperClient.SaveQueueToFileAndClear(cancellationToken);
            if (path is not null)
            {
                Notify($"Saved {count} URL(s) to {Path.GetFileName(path)}", "Save and clear queue",
                    NotificationType.Success);
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Failed to save and clear the queue");
            Notify("Couldn't save the queue; it hasn't been cleared", "Save and clear queue", NotificationType.Error);
        }
    }

    private async Task QueueAndRip(CancellationToken cancellationToken = default)
    {
        var input = UrlInput;
        if (string.IsNullOrWhiteSpace(input) && await RipperClient.GetUrlQueueCount(cancellationToken) == 0)
        {
            return;
        }

        Logger.LogDebug("Queuing URL: {url}", input);

        UrlInput = "";
        _ = Task.Run(() => QueueUrls(input, cancellationToken), cancellationToken);
    }

    private async Task QueueUrls(string input, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(input))
        {
            // Shorthands such as "booru tag1 tag2" are expanded by Core (so the thin client's backend does it too)
            var rejectedUrls = await RipperClient.QueueUrls(input, cancellationToken);

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
                            var response = ReRipBehavior switch
                            {
                                ReRipBehavior.Always => true,
                                ReRipBehavior.Never => false,
                                _ => await ConfirmReripUrl(failedUrl.Url, cancellationToken),
                            };
                            if (ReRipBehavior == ReRipBehavior.Never)
                            {
                                Notify($"Already ripped, skipping: {failedUrl.Url}", "Queue URL");
                            }

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

        Logger.LogDebug("URLS in queue: {count}", await RipperClient.GetUrlQueueCount(cancellationToken));

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

    private async Task Rerip(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            Logger.LogWarning("Cannot re-rip an empty URL.");
            return;
        }

        Logger.LogDebug("Re-ripping URL: {url}", url);
        await RipperClient.ForceQueueUrl(url, cancellationToken);

        Logger.LogDebug("URLS in queue: {count}", await RipperClient.GetUrlQueueCount(cancellationToken));

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
                var queue = (await RipperClient.GetUrlQueue()).ToList();
                var currentUrl = await RipperClient.GetCurrentUrl();
                // Sync in place so the user's selection and scroll position survive queue changes mid-rip
                UrlQueue.SyncTo(queue);
                QueueCount = queue.Count;
                CurrentUrl = currentUrl;
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error occurred while fetching url queue");
            }
        });
    }

    private void OnProgressChanged(int current, int total)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ProgressCurrent = (ulong)Math.Max(current, 0);
            ProgressTotal = (ulong)Math.Max(total, 0);
            UpdateProgressDisplay();
        });
        ProgressChanged?.Invoke(current, total);
    }

    private void OnRipStopped(RipStopReason reason)
    {
        if (reason != RipStopReason.OutOfDiskSpace)
        {
            return;
        }

        SetProgressError();
        Notify("Ripping stopped: not enough disk space. The queue was kept; free up space, then select Rip to continue.",
            "Ripping stopped", NotificationType.Error, 15000);
    }

    private void UpdateProgressDisplay()
    {
        var ripping = CurrentUrl is not null;
        ProgressStripOpacity = ripping ? 1 : 0;
        // Before the first file is counted (e.g. while the page is being parsed) there's nothing to measure
        IsProgressIndeterminate = ripping && ProgressTotal == 0;
        ProgressPercent = ProgressTotal == 0 ? 0 : Math.Min(100, ProgressCurrent * 100.0 / ProgressTotal);
        ProgressText = !ripping ? "" : ProgressTotal == 0 ? "Preparing…" : $"{ProgressCurrent} of {ProgressTotal} files";
    }

    private async Task LoadUnfinishedUrls(CancellationToken cancellationToken = default)
    {
        // Snapshots live in the Core's working directory, which is this process's for the full GUI
        var directory = Path.GetFullPath(NicheImageRipper.Core.NicheImageRipper.UnfinishedUrlsDirectory);
        var files = await Task.Run(() => UnfinishedUrlsViewModel.ScanDirectory(directory), cancellationToken);
        var path = await ShowUnfinishedUrlsDialog.Handle(new UnfinishedUrlsViewModel(directory, files, IsThinClient));
        if (path is null)
        {
            return;
        }

        try
        {
            await RipperClient.LoadUrlFile(path, cancellationToken);
            Notify($"Loaded URLs from {Path.GetFileName(path)}", "Load unfinished URLs", NotificationType.Success);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Failed to load URL list {Path}", path);
            Notify($"Couldn't load {Path.GetFileName(path)}", "Load unfinished URLs", NotificationType.Error);
        }
    }

    /// <summary>Saves the queue, partial-rip state and config (in thin mode, asks the backend to), then GUI settings.</summary>
    public async Task SaveDataAsync()
    {
        try
        {
            await RipperClient.SaveData();
        }
        finally
        {
            GuiSettings.Save();
        }
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
        // Called from the background rip task
        Dispatcher.UIThread.Post(() =>
        {
            ProgressCurrent = 0;
            ProgressTotal = 0;
            ProgressHasError = false;
            ProgressIsPaused = false;
            UpdateProgressDisplay();
        });
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