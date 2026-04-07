using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using Common.Utility;
using Core;
using Core.Enums;
using Core.SiteParsing.HtmlParsers;
using GuiThin.Models;
using GuiThin.Services;
using GuiThin.Views;
using ReactiveUI;
using Serilog;
using Service.Models.Dtos;

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

        var queueAfter = await backendConnector.GetQueueSnapshotAsync();
        Log.Debug("URLS in queue: {Count}", queueAfter.Length);

        if (!await backendConnector.GetIsRippingStateAsync())
        {
            await backendConnector.RipAsync();
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
}