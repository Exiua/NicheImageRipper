using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using Common.Gui.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.History;
using Core.SiteParsing.HtmlParsers;
using Gui.Services;
using ReactiveUI;
using Serilog;

namespace Gui.ViewModels;

public abstract class MainWindowViewModelBase : ViewModelBase
{
    public IRipperClient RipperClient { get; }
    public IRipperSettings RipperSettings { get; }
    public IGuiSettings GuiSettings { get; }
    public ILogTextSource LogTextSource { get; }

    public abstract string Title { get; }
    public abstract int HistoryCount { get; }

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

    public ObservableCollection<string> UrlQueue { get; } = [];
    public ObservableCollection<HistoryEntry> History { get; } = [];

    public List<string> SelectedUrls
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    public ReactiveCommand<Unit, Unit> RipCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearCacheCommand { get; }
    public ReactiveCommand<Unit, Unit> DequeueUrlsCommand { get; }
    public ReactiveCommand<string, Unit> ReRipUrlCommand { get; }
    public Interaction<ConfirmationViewModel, ConfirmationViewModel?> ShowConfirmationDialog { get; } = new();

    protected MainWindowViewModelBase(IRipperClient ripperClient, IRipperSettings ripperSettings,
                                      IGuiSettings guiSettings, ILogTextSource logTextSource)
    {
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

        RipCommand = ReactiveCommand.CreateRunInBackground(QueueAndRip);
        ClearCacheCommand = ReactiveCommand.Create(ClearCache);
        DequeueUrlsCommand = ReactiveCommand.Create(DequeueUrls);
        ReRipUrlCommand = ReactiveCommand.Create<string>(Rerip);
        UrlQueue = new ObservableCollection<string>([]);
        History = new ObservableCollection<HistoryEntry>([]);

        RipperClient.OnUrlQueueUpdated += OnUrlQueueUpdated;
        RipperClient.OnProgressChanged += OnProgressChanged;
    }

    public void Initialize()
    {
        var history = GetHistoryPage(CurrentHistoryPage, PageSize);
        History.Update(history);
    }

    protected abstract void ClearCache();
    protected abstract List<HistoryEntry> GetHistoryPage(int start, int offset, HistoryFilter? filter = null);

    private void OnLogTextChanged(string text)
    {
        Dispatcher.UIThread.Post(() =>
        {
            LogText = text;
        });
    }
    
    private void DequeueUrls()
    {
        RipperClient.DequeueUrls(SelectedUrls);
    }

    private void QueueAndRip()
    {
        var input = UrlInput;
        if (string.IsNullOrWhiteSpace(input) && RipperClient.UrlQueueCount == 0)
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
            var parts = SplitInput(input);
            RejectedUrlsInfo rejectedUrls;
            if (parts[0] == "booru")
            {
                try
                {
                    var urls = ExpandBooruInput(parts);
                    rejectedUrls = RipperClient.QueueUrls(string.Join("", urls));
                }
                catch (InvalidOperationException)
                {
                    Log.Warning("Missing argument: <tags>");
                    return;
                }
            }
            else
            {
                rejectedUrls = RipperClient.QueueUrls(input);
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

                RipperClient.RequeueUrls(rejectedUrls.WithRejectedUrls(urlsToRequeue));
            }
        }

        Log.Debug("URLS in queue: {count}", RipperClient.UrlQueueCount);

        if (RipInProgress)
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

        var result = await ShowConfirmationDialog.Handle(confirmationViewModel);
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

    private void Rerip(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            Log.Warning("Cannot re-rip an empty URL.");
            return;
        }

        Log.Debug("Re-ripping URL: {url}", url);
        RipperClient.ForceQueueUrl(url);

        Log.Debug("URLS in queue: {count}", RipperClient.UrlQueueCount);

        if (RipInProgress)
        {
            return;
        }

        Task.Run(Rip);
    }

    private async Task Rip()
    {
        RipInProgress = true;
        try
        {
            await RipperClient.Rip();

            ClearProgress();
        }
        catch (Exception e)
        {
            Dispatcher.UIThread.Post(() => { Log.Error(e, "Error occurred while ripping"); });
            SetProgressError();
        }
        finally
        {
            RipInProgress = false;
        }
    }

    private void OnUrlQueueUpdated()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var queue = RipperClient.GetUrlQueue().ToList();
            UrlQueue.Update(queue);
            UrlCountText = $"URLs in queue: {queue.Count}";
        });
    }

    private void OnProgressChanged(int current, int total)
    {
        ProgressCurrent = (ulong)current;
        ProgressTotal = (ulong)total;
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
        Log.Debug("CurrentHistoryPage: {CurrentHistoryPage}, PageSize: {PageSize}, HistoryCount: {HistoryCount}",
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

    public void LoadHistory(HistoryFilter? filter = null)
    {
        var history = GetHistoryPage(CurrentHistoryPage - 1, PageSize, filter);
        Log.Debug("History[{Count}]: {@History}", history.Count, history.Count == 0 ? "None" : history[0]);
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

    public bool Resume()
    {
        return RipperClient.Resume();
    }

    public bool Pause()
    {
        return RipperClient.Pause();
    }

    private static void ClearProgress()
    {
        // TODO
    }

    private static void SetProgressError()
    {
        // TODO
    }
}