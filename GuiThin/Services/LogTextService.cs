using System;
using System.Reactive.Subjects;
using GuiThin.Formatters;
using GuiThin.Models;
using GuiThin.Models.Data;

namespace GuiThin.Services;

public sealed class LogTextService : ILogTextService, IDisposable
{
    private readonly RollingLogBuffer _buffer;
    private readonly LogEntryFormatter _formatter;
    private readonly BehaviorSubject<string> _logTextSubject;

    public IObservable<string> LogTextStream => _logTextSubject;

    public LogTextService(LogEntryFormatter formatter, int maxEntries = 500)
    {
        _formatter = formatter;
        _buffer = new RollingLogBuffer(maxEntries);
        _logTextSubject = new BehaviorSubject<string>(string.Empty);
    }

    public void Append(BackendLogEvent entry)
    {
        var formatted = _formatter.Format(entry);
        _buffer.Add(formatted);
        _logTextSubject.OnNext(_buffer.GetCombinedText());
    }

    public void Clear()
    {
        _buffer.Clear();
        _logTextSubject.OnNext(string.Empty);
    }

    public void Dispose()
    {
        _logTextSubject.Dispose();
    }
}