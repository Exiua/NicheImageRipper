using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using NicheImageRipper.Core.DataStructures;
using ReactiveUI;

namespace NicheImageRipper.Gui.ViewModels;

/// <summary>Lists the files the URL being ripped will download, which ones are done, and lets the list be refreshed.</summary>
public class CurrentRipFilesViewModel : ViewModelBase
{
    private readonly Func<CancellationToken, Task<CurrentRipFiles>> _loadFiles;

    public string? RipUrl
    {
        get;
        private set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(EmptyText));
        }
    }

    /// <summary>The rip errored or was interrupted, so these are the files it had left when it stopped.</summary>
    public bool Stopped
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string? DirectoryName
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public IReadOnlyList<RipFileEntry> Files
    {
        get;
        private set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(HasFiles));
            this.RaisePropertyChanged(nameof(Summary));
        }
    } = [];

    public bool HasFiles => Files.Count > 0;

    public string Summary
    {
        get
        {
            var downloaded = Files.Count(f => f.Downloaded);
            var fileCount = Files.Count == 1 ? "1 file" : $"{Files.Count} files";
            return $"{fileCount} · {downloaded} downloaded · {Files.Count - downloaded} remaining";
        }
    }

    public string EmptyText => RipUrl is null
        ? "Nothing is being ripped right now."
        : "The URL is still being parsed, so its files aren't known yet. Refresh in a moment.";

    public ReactiveCommand<CancellationToken, Unit> RefreshCommand { get; }

    public CurrentRipFilesViewModel() : this(CurrentRipFiles.Empty, _ => Task.FromResult(CurrentRipFiles.Empty))
    {
    }

    public CurrentRipFilesViewModel(CurrentRipFiles files, Func<CancellationToken, Task<CurrentRipFiles>> loadFiles)
    {
        _loadFiles = loadFiles;
        Apply(files);
        RefreshCommand = ReactiveCommand.CreateFromTask<CancellationToken>(Refresh);
    }

    private async Task Refresh(CancellationToken cancellationToken)
    {
        Apply(await _loadFiles(cancellationToken));
    }

    private void Apply(CurrentRipFiles files)
    {
        RipUrl = files.Url;
        Stopped = files.Stopped;
        DirectoryName = files.DirectoryName;
        Files = files.Files;
    }
}
