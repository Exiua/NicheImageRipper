using System;
using Gui.Models.Thin.Data;

namespace Gui.Services.Thin;

public interface ILogTextService
{
    IObservable<string> LogTextStream { get; }
    void Append(BackendLogEvent entry);
    void Clear();
}
