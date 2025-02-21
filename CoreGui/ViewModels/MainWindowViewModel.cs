using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Threading;
using Core;
using Core.Enums;
using CoreGui.Utility;
using ReactiveUI;
using Serilog;

namespace CoreGui.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly NicheImageRipper _ripper;
    
    private bool _ripInProgress;
    private string _urlInput = "";
    private string _savePath = NicheImageRipper.SavePath;
    private int _filenameSchemeIndex;
    private int _unzipProtocolIndex;

    public ObservableCollection<string> UrlQueue { get; }
    
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
            NicheImageRipper.FilenameScheme = (FilenameScheme) value;
        }
    }

    public int UnzipProtocolIndex
    {
        get => _unzipProtocolIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _unzipProtocolIndex, value);
            NicheImageRipper.UnzipProtocol = (UnzipProtocol) value;
        }
    }

    public ReactiveCommand<Unit, Unit> RipCommand { get; }

    public MainWindowViewModel()
    {
        _ripper = new NicheImageRipper();
        RipCommand = ReactiveCommand.Create(QueueAndRip);   
        UrlQueue = new ObservableCollection<string>(_ripper.UrlQueue);
        
        _ripper.OnUrlQueueUpdated += OnUrlQueueUpdated;
    }
    
    private void OnUrlQueueUpdated()
    {
        Dispatcher.UIThread.Post(() => UrlQueue.Update(_ripper.UrlQueue));
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
        _ripper.QueueUrls(input);
        
        Log.Debug("URLS in queue: {count}", _ripper.UrlQueue.Count);
    
        if (_ripInProgress)
        {
            return;
        }
        
        Task.Run(Rip);
    }
    
    private async Task Rip()
    {
        _ripInProgress = true;
        try
        {
            await _ripper.Rip();
        }
        catch (Exception e)
        {
            Dispatcher.UIThread.Post(() => { Log.Error(e, "Error occurred while ripping"); });
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
}