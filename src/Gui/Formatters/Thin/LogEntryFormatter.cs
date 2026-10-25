using System.Text;
using NicheImageRipper.Gui.Models;

namespace NicheImageRipper.Gui.Formatters.Thin;

public sealed class LogEntryFormatter
{
    public string Format(LogEntryModel entry)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(entry.Timestamp?.ToString("0")))
        {
            sb.Append('[');
            sb.Append(entry.Timestamp);
            sb.Append("] ");
        }

        if (!string.IsNullOrWhiteSpace(entry.Level))
        {
            sb.Append(entry.Level);
            sb.Append(": ");
        }

        sb.Append(entry.Message ?? entry.MessageTemplate ?? "<no message>");

        if (!string.IsNullOrWhiteSpace(entry.Exception))
        {
            sb.AppendLine();
            sb.Append(entry.Exception);
        }

        return sb.ToString();
    }
}