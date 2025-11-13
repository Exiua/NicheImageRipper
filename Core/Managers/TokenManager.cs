using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Configuration;
using Core.DataStructures;
using Core.Enums;
using Core.Utility;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Util.Store;

namespace Core.Managers;

/// <summary>
///     Class for managing tokens that are dynamically generated (e.g., redgif tokens)
/// </summary>
public class TokenManager
{
    private const string TokenPath = "temp_tokens.json";
    
    private static GeneralConfig Config => Configuration.Config.Instance;
    
    public static TokenManager Instance { get; } = new();

    private TokenState TokenState { get; }
    
    private TokenManager()
    {
        TokenState = File.Exists(TokenPath)
            ? JsonUtility.Deserialize<TokenState>(TokenPath)!
            : new TokenState();
    }

    private void SaveTokens()
    {
        JsonUtility.Serialize(TokenPath, TokenState);
    }

    public async Task<Token> GetToken(TokenKey key)
    {
        if (TokenState.Tokens.TryGetValue(key, out var token) && token?.Expiration >= DateTime.Now)
        {
            return token;
        }

        token = await GenerateToken(key);
        TokenState.Tokens.Redgifs = token;
        SaveTokens();
        return token;
    }

    public string GetTokenWithRotation(RotationKey key, TimeSpan maxDuration, string[] tokens)
    {
        var rotation = TokenState.Rotations.TryGetValue(key, out var rot)
            ? rot!
            : throw new KeyNotFoundException($"Unknown rotation key: {key}");
        
        var span = rotation.Paused ? TimeSpan.Zero : DateTime.Now - rotation.LastUsed;
        var usedDuration = span + rotation.UsedDuration;
        int currentIndex;
        if (usedDuration >= maxDuration)
        {
            rotation.UsedDuration = TimeSpan.Zero;
            currentIndex = (rotation.CurrentIndex + 1) % tokens.Length;
            rotation.CurrentIndex = currentIndex;
        }
        else
        {
            rotation.UsedDuration = usedDuration;
            currentIndex = rotation.CurrentIndex;
        }
        
        rotation.LastUsed = DateTime.Now;
        rotation.Paused = false;
        SaveTokens();
        return tokens[currentIndex];
    }

    public void UpdateTokenRotation(RotationKey key)
    {
        var rotation = TokenState.Rotations.TryGetValue(key, out var rot)
            ? rot!
            : throw new KeyNotFoundException($"Unknown rotation key: {key}");
        
        var span = DateTime.Now - rotation.LastUsed;
        rotation.UsedDuration += span;
        rotation.LastUsed = DateTime.Now;
        rotation.Paused = true;
        SaveTokens();
    }

    private static async Task<Token> GenerateToken(TokenKey key)
    {
        return key switch
        {
            TokenKey.Redgifs => await GenerateRedgifsToken(),
            _ => throw new KeyNotFoundException($"Unknown token key: {key}")
        };
    }

    private static async Task<Token> GenerateRedgifsToken()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
        var response = await client.GetAsync("https://api.redgifs.com/v2/auth/temporary");
        if(!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Failed to get Redgifs token");
        }
        var json = (await response.Content.ReadFromJsonAsync<JsonNode>())!;
        var expiration = DateTime.Now + TimeSpan.FromHours(24);
        var token = json["token"].Deserialize<string>();
        return token is null
            ? throw new InvalidOperationException("Failed to get Redgifs token")
            : new Token(token, expiration);
    }

    public static async Task<UserCredential> GDriveAuthenticate()
    {
        await using var stream = new FileStream("client_secrets.json", FileMode.Open, FileAccess.Read);
        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            (await GoogleClientSecrets.FromStreamAsync(stream)).Secrets,
            [DriveService.Scope.DriveReadonly],
            "user", CancellationToken.None, new FileDataStore("GDriveCredentialCache"));

        return credential;
    }
}