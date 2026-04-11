using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using Common.Gui.Utility;
using Core;
using Core.DataStructures;
using Core.Enums;
using Core.History;
using Core.SiteParsing.HtmlParsers;
using Gui.Models;
using Gui.Services;
using Gui.Views;
using ReactiveUI;
using Serilog;

namespace Gui.ViewModels;

public class MainWindowViewModel : MainWindowViewModelBase
{
    private static readonly Version Version = new(1, 0, 0);
    private static GuiConfig Config => (GuiConfig) Core.Configuration.Config.Instance;

    public override string Title => $"Gui v{Version} - Core v{NicheImageRipper.Version}";

    internal MainWindow MainWindow { get; set; } = null!;

    private bool _ripInProgress;

    public string SavePath
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            NicheImageRipper.SavePath = value;
        }
    } = NicheImageRipper.SavePath;

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

    static MainWindowViewModel()
    {
        // FIXME: There is probably a cleaner way to structure this helper function
        GuiUtility.ExtractTagsFromUrlFunc = BooruParser.ExtractTagsFromUrl;
    }
    
    public MainWindowViewModel(IRipperClient ripperClient, IRipperSettings ripperSettings, IGuiSettings guiSettings) : base(ripperClient, ripperSettings, guiSettings)
    {
    }

    protected override void ClearCache()
    {
        NicheImageRipper.ClearCache();
    }

    protected override List<HistoryEntry> GetHistoryPage(int start, int offset, HistoryFilter? filter = null) => NicheImageRipper.GetHistoryPage(start, offset, filter);

    protected override void DequeueUrls()
    {
        RipperClient.DequeueUrls(SelectedUrls);
    }

    protected override void Rerip(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            Log.Warning("Cannot re-rip an empty URL.");
            return;
        }
        
        Log.Debug("Re-ripping URL: {url}", url);
        RipperClient.ForceQueueUrl(url);
        
        Log.Debug("URLS in queue: {count}", RipperClient.UrlQueueCount);

        if (_ripInProgress)
        {
            return;
        }

        Task.Run(Rip);
    }

    protected override void QueueAndRip()
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
            var parts = GuiUtility.SplitInput(input);
            RejectedUrlsInfo rejectedUrls;
            if (parts[0] == "booru")
            {
                try
                {
                    var urls = GuiUtility.ExpandBooruInput(parts);
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
        
        var result = await ShowConfirmationDialog.Handle(confirmationViewModel);
        return result?.Confirmed ?? false;
    }

    private async Task Rip()
    {
        _ripInProgress = true;
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
            _ripInProgress = false;
        }
    }

    public override void LoadUnfinishedUrls(string path)
    {
        RipperClient.LoadUrlFile(path);
    }

    public override void SaveData()
    {
        RipperClient.SaveData();
    }

    public override void Cleanup()
    {
        RipperClient.Dispose();
    }
}