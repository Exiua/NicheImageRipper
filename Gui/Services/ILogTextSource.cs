using System;
using NicheImageRipper.Gui.Models;

namespace NicheImageRipper.Gui.Services;

public interface ILogTextSource
{
    string CurrentText { get; }
    void Append(LogEntryModel entry);
    void Clear();
    event Action<string>? LogTextChanged;
}