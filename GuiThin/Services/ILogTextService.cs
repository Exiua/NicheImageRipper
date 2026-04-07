using System;
using GuiThin.Models.Data;

namespace GuiThin.Services;

public interface ILogTextService
{
    IObservable<string> LogTextStream { get; }
    void Append(BackendLogEvent entry);
    void Clear();
}
