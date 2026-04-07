using System.Text;
using GuiThin.Models.Data;

namespace GuiThin.Formatters;

public sealed class LogEntryFormatter
{
    public string Format(BackendLogEvent entry)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(entry.Timestamp))
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

        sb.Append(entry.RenderedMessage ?? entry.MessageTemplate ?? "<no message>");

        if (!string.IsNullOrWhiteSpace(entry.Exception))
        {
            sb.AppendLine();
            sb.Append(entry.Exception);
        }

        return sb.ToString();
    }
}