using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using Avalonia.Threading;
using Common.Gui.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.History;
using Gui.Models;
using Gui.Services;
using ReactiveUI;
using Serilog;

namespace Gui.ViewModels;

public abstract class MainWindowViewModelBase : ViewModelBase
{
    protected readonly IRipperClient RipperClient;

    public IRipperSettings RipperSettings { get; private set; }
    public IGuiSettings GuiSettings { get; private set; }

    public abstract string Title { get; }

    public string UrlInput
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";

    public string LogText
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";
    
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
    public int HistoryCount
    {
        get;
        protected set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public int PageSize
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = 100;

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

    protected MainWindowViewModelBase(IRipperClient ripperClient, IRipperSettings ripperSettings, IGuiSettings guiSettings)
    {
        RipperClient = ripperClient;
        
        RipperSettings = ripperSettings;
        MaxRetriesDisplay = RipperSettings.MaxRetries.ToString();
        RetryDelayDisplay = RipperSettings.RetryDelay.ToString();
        
        GuiSettings = guiSettings;
        
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

    protected abstract void QueueAndRip();
    protected abstract void ClearCache();
    protected abstract void DequeueUrls();
    protected abstract void Rerip(string url);
    protected abstract List<HistoryEntry> GetHistoryPage(int start, int offset, HistoryFilter? filter = null);

    protected void OnUrlQueueUpdated()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var queue = RipperClient.GetUrlQueue().ToList();
            UrlQueue.Update(queue);
            UrlCountText = $"URLs in queue: {queue.Count}";
        });
    }

    protected void OnProgressChanged(int current, int total)
    {
        ProgressCurrent = (ulong)current;
        ProgressTotal = (ulong)total;
    }
    
    public abstract void SaveData();
    public abstract void Cleanup();
    public abstract void LoadUnfinishedUrls(string path);
    
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

    public void ClearProgress()
    {
        // TODO
    }

    public void SetProgressError()
    {
        // TODO
    }
}