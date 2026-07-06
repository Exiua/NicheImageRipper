using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ImpersonateClient;
using IwaraApiClient.Models;
using Serilog;
using ILogger = Serilog.ILogger;

namespace IwaraApiClient;

public class IwaraClient
{
    private readonly ILogger _logger = Log.ForContext<IwaraClient>();
    private readonly string _username;
    private readonly string _password;
    private readonly ImpersonateHttpClient _httpClient;

    private string _token = "";
    private DateTime _expiration = DateTime.MinValue;

    public IwaraClient(string username, string password)
    {
        _httpClient = ImpersonateHttpClient.Builder()
            .WithBrowser("chrome".IntoImpersonateTarget())
            .Build();
        _username = username;
        _password = password;
    }

    private async Task<bool> CheckToken(CancellationToken cancellationToken = default)
    {
        if (_token == "" || DateTime.UtcNow >= _expiration)
        {
            return await Login(cancellationToken);
        }

        if (DateTime.UtcNow + TimeSpan.FromSeconds(30) >= _expiration)
        {
            // Refresh token
        }

        return true;
    }

    private async Task<bool> Login(CancellationToken cancellationToken = default)
    {
        var request = new LoginRequest
        {
            Email = _username,
            Password = _password
        };
        var response = await _httpClient.PostAsJsonAsync("https://api.iwara.tv/user/login", request,
            cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.Warning("Login failed.");
            return false;
        }

        var loginResponse =
            await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: cancellationToken);
        if (loginResponse is null)
        {
            _logger.Warning("Login failed.");
            return false;
        }

        _token = loginResponse.Token;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(_token);
        _expiration = jwt.ValidTo;

        _logger.Debug("Successfully logged in.");
        return true;
    }

    public async Task<GetUserResponse?> GetUser(string username, CancellationToken cancellationToken = default)
    {
        var success = await CheckToken(cancellationToken);
        if (!success)
        {
            return null;
        }

        var requestUrl = $"https://api.iwara.tv/profile/{username}";
        var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var user = await response.Content.ReadFromJsonAsync<GetUserResponse>(cancellationToken: cancellationToken);
        return user;
    }

    // Page is 0-based
    public async Task<PagedResponse<Video>?> GetUserVideos(Guid userId, int page,
                                                           ContentRating rating = ContentRating.All,
                                                           ContentSort sortBy = ContentSort.Date,
                                                           CancellationToken cancellationToken = default)
    {
        var success = await CheckToken(cancellationToken);
        if (!success)
        {
            return null;
        }

        var requestUrl =
            $"https://api.iwara.tv/videos?rating={rating.ToUrlString()}&sort={sortBy.ToUrlString()}&page={page}&user={userId}";
        var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var pagedVideos =
            await response.Content.ReadFromJsonAsync<PagedResponse<Video>>(cancellationToken: cancellationToken);
        return pagedVideos;
    }

    public async Task<PagedResponse<Image>?> GetUserImages(Guid userId, int page,
                                                           ContentRating rating = ContentRating.All,
                                                           ContentSort sortBy = ContentSort.Date,
                                                           CancellationToken cancellationToken = default)
    {
        var success = await CheckToken(cancellationToken);
        if (!success)
        {
            return null;
        }

        var requestUrl =
            $"https://api.iwara.tv/images?rating={rating.ToUrlString()}&user={userId}&page={page}&sort={sortBy.ToUrlString()}";
        var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var pagedImages =
            await response.Content.ReadFromJsonAsync<PagedResponse<Image>>(cancellationToken: cancellationToken);
        return pagedImages;
    }

    public async Task<Video?> GetVideo(string videoId, CancellationToken cancellationToken = default)
    {
        var success = await CheckToken(cancellationToken);
        if (!success)
        {
            return null;
        }

        var requestUrl = $"https://api.iwara.tv/video/{videoId}";
        var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var video = await response.Content.ReadFromJsonAsync<Video>(cancellationToken: cancellationToken);
        return video;
    }

    public static string CalculateXVersion(string videoGuid, string expirationTimestamp)
    {
        const string secret = "mSvL05GfEmeEmsEYfGCnVpEjYgTJraJN";
        var input = string.Join("_", videoGuid, expirationTimestamp, secret);
        var hash = System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        var xVersion = Convert.ToHexStringLower(hash);
        return xVersion;
    }

    public async Task DownloadVideo(string videoId)
    {
        throw new NotImplementedException();
    }
}