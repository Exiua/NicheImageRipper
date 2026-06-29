namespace NicheImageRipper.Core.Clients.Iwara.Models;

public enum ContentRating
{
    All = 0,
    General = 1,
    Ecchi = 2,
}

public static class ContentRatingExtensions
{
    public static string ToUrlString(this ContentRating rating)
    {
        return rating switch
        {
            ContentRating.All => "all",
            ContentRating.General => "general",
            ContentRating.Ecchi => "ecchi",
            _ => throw new ArgumentOutOfRangeException(nameof(rating), rating, null)
        };
    }
}