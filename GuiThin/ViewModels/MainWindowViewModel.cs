using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Common.Gui.ExtensionMethods;
using Common.Gui.Utility;
using Core;
using Core.DataStructures;
using Core.Enums;
using Core.History;
using Core.SiteParsing.HtmlParsers;
using GuiThin.Models;
using GuiThin.Services;
using GuiThin.Views;
using ReactiveUI;
using Serilog;
using Service.Models.Dtos;
using Service.Models.Requests;

namespace GuiThin.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private static readonly Version Version = new(1, 0, 0);

    public static string Title => $"GuiThin v{Version} - Core v{NicheImageRipper.Version}";
    private static GuiThinConfig Config => (GuiThinConfig)Core.Configuration.Config.Instance;
    
    internal MainWindow MainWindow { get; set; } = null!;

    private readonly IDisposable _logSubscription;
    private readonly IBackendConnector backendConnector;

    public string UrlInput
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";

    public string LogText
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool Paused
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }
    
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
    
    public List<string> SelectedUrls { get; set; } = [];
    
    public int CurrentHistoryPage { get; private set; } = 1;

    public string CurrentHistoryPageDisplay
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "1";

    public int HistoryPageSize { get; set; } = 100;
    
    public ObservableCollection<string> UrlQueue { get; }
    public ObservableCollection<HistoryEntry> History { get; }

    public ReactiveCommand<Unit, Task> RipCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearCacheCommand { get; }
    public ReactiveCommand<Unit, Unit> DequeueUrlsCommand { get; }
    public ReactiveCommand<string, Unit> ReRipUrlCommand { get; }

    static MainWindowViewModel()
    {
        // FIXME: There is probably a cleaner way to structure this helper function
        GuiUtility.ExtractTagsFromUrlFunc = BooruParser.ExtractTagsFromUrl;
    }
    
    public MainWindowViewModel(IBackendConnector backendConnector, ILogTextService logTextService)
    {
        this.backendConnector = backendConnector;
        _logSubscription = logTextService.LogTextStream
                                         .ObserveOn(RxSchedulers.MainThreadScheduler)
                                         .Subscribe(text => { LogText = text; });
        RipCommand = ReactiveCommand.CreateRunInBackground(QueueAndRipAsync);
        ClearCacheCommand = ReactiveCommand.Create(ClearCache);
        DequeueUrlsCommand = ReactiveCommand.Create(DequeueUrls);
        ReRipUrlCommand = ReactiveCommand.Create<string>(Rerip);
        
        UrlQueue = new ObservableCollection<string>([]);
        History = new ObservableCollection<HistoryEntry>([]);
    }

    private async Task QueueAndRipAsync()
    {
        var input = UrlInput;

        if (string.IsNullOrWhiteSpace(input))
        {
            var queue = await backendConnector.GetQueueSnapshotAsync();
            if (queue.Length == 0)
            {
                return;
            }
        }

        Log.Debug("Queuing URL: {Url}", input);
        UrlInput = string.Empty;

        var urlsToQueue = BuildUrlsToQueue(input);
        await QueueUrlsCoreAsync(urlsToQueue);

        await RefreshQueueCountAsync();

        if (!await backendConnector.GetIsRippingStateAsync())
        {
            await backendConnector.RipAsync();
            await RefreshQueueCountAsync();
        }
    }
    
    private static List<string> BuildUrlsToQueue(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return [];
        }

        var parts = GuiUtility.SplitInput(input);

        if (parts.Count <= 0 || parts[0] != "booru")
        {
            return parts;
        }

        if (parts.Count < 2)
        {
            throw new InvalidOperationException("Missing argument: <tags>");
        }

        return GuiUtility.ExpandBooruInput(parts);
    }
    
    private async Task QueueUrlsCoreAsync(List<string> urls)
    {
        if (urls.Count == 0)
        {
            return;
        }

        var rejectedUrls = await backendConnector.QueueUrlsAsync(urls.ToArray());
        await HandleRejectedUrlsAsync(rejectedUrls);
    }
    
    private async Task HandleRejectedUrlsAsync(List<RejectedUrlInfoDto> rejectedUrls)
    {
        if (rejectedUrls.Count == 0)
        {
            return;
        }

        var urlsToRequeue = new List<RejectedUrlInfoDto>();

        foreach (var failedUrl in rejectedUrls)
        {
            switch (failedUrl.Reason)
            {
                case QueueFailureReason.None:
                {
                    break;
                }
                case QueueFailureReason.AlreadyQueued:
                {
                    Log.Information("URL already queued: {Url}", failedUrl.Url);
                    break;
                }
                case QueueFailureReason.NotSupported:
                {
                    Log.Warning("URL not supported: {Url}", failedUrl.Url);
                    break;
                }
                case QueueFailureReason.PreviouslyProcessed:
                {
                    Log.Information("Re-rip url? {Url}", failedUrl.Url);

                    var response = await ConfirmReripUrl(failedUrl.Url);
                    if (response)
                    {
                        urlsToRequeue.Add(failedUrl);
                        Log.Debug("Re-ripping URL: {Url}", failedUrl.Url);
                    }
                    else
                    {
                        Log.Debug("Skipping re-rip for URL: {Url}", failedUrl.Url);
                    }

                    break;
                }
                default:
                {
                    throw new InvalidOperationException(
                        "Invalid QueueFailureReason: " + failedUrl.Reason);
                }
            }
        }

        if (urlsToRequeue.Count > 0)
        {
            await backendConnector.QueueUrlsAsync(urlsToRequeue.Select(u => u.Url).ToArray());
        }
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
    
    public async Task RefreshQueueCountAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await backendConnector.GetQueueSnapshotAsync(cancellationToken);
        UrlCountText = $"URLs in queue: {snapshot.Length}";
    }
    
    public async Task<bool> PlayAsync(CancellationToken cancellationToken = default)
    {
        var stateChanged = await backendConnector.ResumeAsync(cancellationToken);
        Paused = false;
        return stateChanged;
    }

    public async Task<bool> PauseAsync(CancellationToken cancellationToken = default)
    {
        var stateChanged = await backendConnector.PauseAsync(cancellationToken);
        Paused = true;
        return stateChanged;
    }

    public void ApplyCurrentHistoryPageDisplay()
    {
        if (int.TryParse(CurrentHistoryPageDisplay, out var page) && page >= 1)
        {
            CurrentHistoryPage = page;
        }
        else
        {
            CurrentHistoryPage = 1;
            CurrentHistoryPageDisplay = "1";
        }
    }
    
    public async Task RefreshHistoryPageAsync(CancellationToken cancellationToken = default)
    {
        ApplyCurrentHistoryPageDisplay();

        var historyCount = await backendConnector.GetHistoryCountAsync(cancellationToken);
        var maxPage = Math.Max(1, (int)Math.Ceiling(historyCount / (double)HistoryPageSize));

        if (CurrentHistoryPage > maxPage)
        {
            CurrentHistoryPage = maxPage;
            CurrentHistoryPageDisplay = maxPage.ToString();
        }

        var request = new GetHistoryRequest
        {
            Start = (CurrentHistoryPage - 1) * HistoryPageSize,
            Offset = HistoryPageSize,
            Filter = BuildHistoryFilter()
        };

        var history = await backendConnector.GetHistoryAsync(request, cancellationToken);

        History.Update(history);
    }

    private HistoryFilter? BuildHistoryFilter()
    {
        var input = HistoryFilterText.Trim();
        return string.IsNullOrEmpty(input) ? null : HistoryFilter.Parse(input);
    }

    public async Task RefreshStateAsync(CancellationToken cancellationToken = default)
    {
        Paused = await backendConnector.GetPausedStateAsync(cancellationToken);
        await RefreshQueueCountAsync(cancellationToken);
    }
}