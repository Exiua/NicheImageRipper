using System.Text.Json;

namespace Core;

public class TokenManager
{
    private const string TokenPath = "temp_tokens.json";
    
    private static TokenManager? _instance;
    
    public static TokenManager Instance => _instance ??= new TokenManager();

    public Dictionary<string, Token> Tokens { get; init; }
    
    private TokenManager()
    {
        Tokens = File.Exists(TokenPath)
            ? JsonUtility.Deserialize<Dictionary<string, Token>>(TokenPath)
            : new Dictionary<string, Token>();
    }

    private void SaveTokens()
    {
        JsonUtility.Serialize(TokenPath, Tokens);
    }

    public async Task<Token> GetToken(string key)
    {
        Tokens.TryGetValue(key, out var token);
        if (token is not null && token.Expiration >= DateTime.Now)
        {
            return token;
        }

        token = await GenerateToken(key);
        Tokens[key] = token;
        SaveTokens();
        return token;
    }

    private async Task<Token> GenerateToken(string key)
    {
        return key switch
        {
            "redgifs" => await GenerateRedgifsToken(),
            _ => throw new KeyNotFoundException($"Unknown token key: {key}")
        };
    }

    private async Task<Token> GenerateRedgifsToken()
    {
        var client = new HttpClient();
        var response = await client.GetAsync("https://api.redgifs.com/v2/auth/temporary");
        var jsonStream = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(jsonStream);
        var expiration = DateTime.Now + TimeSpan.FromHours(24);
        var token = json.RootElement.GetProperty("token").GetString();
        if (token is null)
        {
            throw new InvalidOperationException("Failed to get Redgifs token");
        }
        
        return new Token(token, expiration);
    }
}