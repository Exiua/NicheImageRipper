using System.Web;
using Core.History;

namespace Service.Models.Requests;

public class GetHistoryRequest
{
    public int Start { get; set; }
    public int Offset { get; set; }
    public HistoryFilter? Filter { get; set; }

    public string ToQuery()
    {
        var query = HttpUtility.ParseQueryString(string.Empty);

        query["start"] = Start.ToString();
        query["offset"] = Offset.ToString();

        if (Filter is not null)
        {
            query["filterType"] = Filter.FilterType.ToString();

            query["filterValue"] = Filter switch
            {
                HistoryDateFilter d => d.Date.ToString("O"),
                HistoryUrlFilter u => u.Url,
                HistoryNameFilter n => n.Name,
                _ => throw new InvalidOperationException()
            };
        }

        return "?" + query;
    }
}