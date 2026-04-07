using System.Text.RegularExpressions;

namespace Core.History;

public abstract partial class HistoryFilter
{
    public abstract HistoryFilterType FilterType { get; }
    
    public static HistoryFilter? Parse(string input)
    { 
        HistoryFilter? filter = null;
        var match = HistoryFilterRegex().Match(input);
        if (match.Success)
        {
            var category = match.Groups[1].Value;
            var value = match.Groups[2].Value;
            switch (category)
            {
                case "name":
                    filter = new HistoryNameFilter(value);
                    break;
                case "url":
                    filter = new HistoryUrlFilter(value);
                    break;
                case "date":
                    var date = ParsePartialDate(value);
                    if (date is not null)
                    {
                        filter = new HistoryDateFilter(date.Value);
                    }
                    break;
                case "before":
                    date = ParsePartialDate(value);
                    if (date is not null)
                    {
                        filter = new HistoryDateFilter(date.Value, HistoryFilterType.DateEnd);
                    }
                    break;
                case "after":
                    date = ParsePartialDate(value);
                    if (date is not null)
                    {
                        filter = new HistoryDateFilter(date.Value, HistoryFilterType.DateStart);
                    }
                    break;
            }
        }
        else
        {
            filter = new HistoryNameFilter(input);
        }

        return filter;
    }

    private static DateTime? ParsePartialDate(string input)
    {
        var formats = new[]
        {
            "yyyy",       // e.g., "2025" → 2025/01/01
            "MM",         // e.g., "02"   → currentYear/02/01
            "yyyy/MM",    // e.g., "2025/02" → 2025/02/01
            "yyyy-MM",    // e.g., "2025-02"
            "MM/yyyy",    // e.g., "02/2025"
            "MM-yyyy",    // e.g., "02-2025"
            "yyyy/MM/dd", // full date fallback
            "MM/dd/yyyy"
        };

        var now = DateTime.Now;

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(input, format, null, System.Globalization.DateTimeStyles.None, out var result))
            {
                return format switch
                {
                    // Fill in missing components manually
                    "yyyy" => new DateTime(result.Year, 1, 1),
                    "MM" => new DateTime(now.Year, result.Month, 1),
                    "yyyy/MM" or "yyyy-MM" or "MM/yyyy" or "MM-yyyy" => new DateTime(result.Year, result.Month, 1),
                    _ => result
                };
            }
        }

        return null;
    }
    
    [GeneratedRegex(@"^(?i)(name|url|date|before|after):(.+)", RegexOptions.None, "en-US")]
    private static partial Regex HistoryFilterRegex();
}