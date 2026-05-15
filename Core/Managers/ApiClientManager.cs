using PixivApi;

namespace Core.Managers;

// Use this class to hold api clients that need to make auth requests to external services
public class ApiClientManager
{
    public PixivApiClient PixivClient { get; set; } = new();
    public SteamApiClient.SteamApiClient SteamApiClient { get; set; } = new();
}