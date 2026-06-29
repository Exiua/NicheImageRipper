namespace NicheImageRipper.Core.Clients.Iwara.Models;

public enum ContentSort
{
    Date = 0,
    Trending = 1,
    Popularity = 2,
    Views = 3,
    Likes = 4,
}

public static class ContentSortExtensions
{
    public static string ToUrlString(this ContentSort sort)
    {
        return sort switch
        {
            ContentSort.Date => "date",
            ContentSort.Trending => "trending",
            ContentSort.Popularity => "popularity",
            ContentSort.Views => "views",
            ContentSort.Likes => "likes",
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, null)
        };
    }
}