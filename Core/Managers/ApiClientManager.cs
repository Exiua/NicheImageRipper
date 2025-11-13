using PixivApi;

namespace Core.Managers;

public class ApiClientManager
{
    public PixivApiClient PixivClient { get; set; } = new();
}