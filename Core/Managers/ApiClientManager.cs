using IwaraApiClient;
using NicheImageRipper.Core.Configuration;
using PixivApi;

namespace NicheImageRipper.Core.Managers;

// Use this class to hold api clients that need to make auth requests to external services
public class ApiClientManager
{
    private static GeneralConfig Config => Configuration.Config.Instance;
    
    public SteamApiClient.SteamApiClient SteamApiClient { get; set; } = new();
    public IwaraClient IwaraClient { get; set; } = new(Config.Logins.GetValueOrDefault("iwara").Username, Config.Logins.GetValueOrDefault("iwara").Password);
}