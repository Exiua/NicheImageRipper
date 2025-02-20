using System;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Threading;
using Core;
using ReactiveUI;
using Serilog;

namespace CoreGui.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly NicheImageRipper _ripper;
    
    private bool _ripInProgress;
    private string _urlInput = "";
    
    public string UrlInput
    {
        get => _urlInput;
        set => this.RaiseAndSetIfChanged(ref _urlInput, value);
    }
    
    public ReactiveCommand<Unit, Unit> RipCommand { get; }

    public MainWindowViewModel()
    {
        _ripper = new NicheImageRipper();
        //RipCommand = ReactiveCommand.Create(QueueAndRip);   
    }
    //
    // private void QueueAndRip()
    // {
    //     var input = UrlInput;
    //     if (string.IsNullOrWhiteSpace(input))
    //     {
    //         return;
    //     }
    //     
    //     UrlInput = "";
    //     _ripper.QueueUrls(input);
    //
    //     if (_ripInProgress)
    //     {
    //         return;
    //     }
    //     
    //     Task.Run(Rip);
    // }
    //
    // private async Task Rip()
    // {
    //     _ripInProgress = true;
    //     try
    //     {
    //         await _ripper.Rip();
    //     }
    //     catch (Exception e)
    //     {
    //         Dispatcher.UIThread.Post(() => { Log.Error(e, "Error occurred while ripping"); });
    //     }
    //     finally
    //     {
    //         _ripInProgress = false;
    //     }
    // }
}