using System;
using Gui.Models;

namespace Gui.Services;

public interface ILogTextSource
{
    string CurrentText { get; }
    void Append(LogEntryModel entry);
    void Clear();
    event Action<string>? LogTextChanged;
}